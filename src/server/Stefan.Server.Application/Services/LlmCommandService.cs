using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Stefan.Server.Application.Tools;

namespace Stefan.Server.Application.Services;

public class LlmCommandService(
    ChatClient chatClient,
    ToolRegistry toolRegistry,
    ILogger<LlmCommandService> logger) : ILlmCommandService
{
    private static string BuildSystemPrompt() => $"""
        You are Stefan, a helpful voice assistant. You can answer general-knowledge questions and use the available tools to manage timers and the ingredient list.

        Timer requests:
        - Use the timer tools to create, list, or cancel timers. Use the listing tool for current timer state; do not rely on memory.
        - When cancelling a timer by name, look up the active timers first, then use the matching timer ID.
        - Do not claim an action succeeded unless the tool result confirms it. Say durations in human-friendly terms, such as "5 minutes" rather than "300 seconds."

        Ingredient list requests:
        - The user narrates the ingredients they add while cooking one dish, so the list records what went into that dish.
        - Use the ingredient tools for every change to the list. Use the add tool for a new ingredient, the update tool when changing the amount of an ingredient that is already on the list, and the remove tool to delete an ingredient from the list. Keep quantities exactly as spoken, such as "200g" or "2 pieces"; omit the quantity on add when none was said. The update tool always needs the new quantity; if the user did not say one, do not call it.
        - Read the list with the listing tool before updating or removing.
        - Clear the list when the user starts a new dish or asks to clear it.
        - Do not claim an action succeeded unless the tool result confirms it.

        General questions:
        - Answer general-knowledge questions directly.
        - If you are unsure, or the answer depends on current information you do not have, say so briefly. Do not invent facts.

        No follow-up questions:
        - Never ask the user questions or request more information.
        - If required information is missing, or the request is too ambiguous to act on, do not guess. Briefly state why you cannot complete it.
        - Make an assumption only when it is obvious, safe, and does not materially change the request.

        Spoken responses:
        - Keep replies concise and natural for text-to-speech. Prefer one short sentence; use a second only when needed.
        - Avoid markdown, bullets, code, emojis, unusual symbols, and abbreviations. Present results as natural speech.

        The current date and time is {DateTime.Now:dddd, MMMM d, yyyy h:mm tt}.
        """;

    private const int MaxToolCallIterations = 8;

    // TODO: remove async from name
    public async Task<Result<LlmCommandResult>> ProcessCommandAsync(string command, string deviceId, Guid commandId, CancellationToken cancellationToken = default, Action<IReadOnlyList<ConversationMessage>>? onConversationUpdated = null)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var systemPrompt = BuildSystemPrompt();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(command),
        ];

        var conversationMessages = new List<ConversationMessage>
        {
            new("system", systemPrompt, null),
            new("user", command, null),
        };

        Action notifyConversation = () => onConversationUpdated?.Invoke(conversationMessages.ToList());
        notifyConversation();

        var toolCallContext = new ToolCallContext()
        {
            SourceDeviceId = deviceId,
            CommandId = commandId
        };
        var toolCallIterations = 0;
        bool requiresAction;

        do
        {
            logger.LogInformation("Sending command to LLM (message count: {MessageCount})...", messages.Count);
            requiresAction = false;
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, GetChatCompletionOptions(), cancellationToken);

            logger.LogInformation("Received response from LLM (finish reason: {FinishReason})", completion.FinishReason);

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        messages.Add(new AssistantChatMessage(completion));
                        var assistantMessage = completion.Content[0].Text;
                        logger.LogInformation("Assistant response: {AssistantMessage}", assistantMessage);
                        conversationMessages.Add(new ConversationMessage("assistant", assistantMessage, null));
                        notifyConversation();
                        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                        
                        return new LlmCommandResult(assistantMessage, conversationMessages, durationMs);
                    }

                case ChatFinishReason.ToolCalls:
                    {
                        logger.LogInformation("LLM requested tool calls: {ToolCalls}", string.Join(", ", completion.ToolCalls.Select(c => c.FunctionName)));

                        if (++toolCallIterations > MaxToolCallIterations)
                        {
                            logger.LogWarning("Tool call limit of {MaxToolCallIterations} exceeded, aborting command", MaxToolCallIterations);
                            var overflowToolCalls = completion.ToolCalls.Select(toolCall => new ToolCallRecord(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments.ToString(), null)).ToList();
                            conversationMessages.Add(new ConversationMessage("assistant", null, overflowToolCalls));
                            notifyConversation();
                            return Result<LlmCommandResult>.Failure($"Model exceeded the maximum of {MaxToolCallIterations} tool call iterations.");
                        }

                        messages.Add(new AssistantChatMessage(completion.ToolCalls));

                        var toolCalls = new List<ToolCallRecord>();

                        foreach (ChatToolCall toolCall in completion.ToolCalls)
                        {
                            logger.LogInformation("Tool call: {ToolName} with arguments {ToolArguments}", toolCall.FunctionName, toolCall.FunctionArguments);
                            var toolResult = await DispatchToolCallAsync(toolCall, toolCallContext, cancellationToken);
                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));

                            toolCalls.Add(new ToolCallRecord(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments.ToString(), toolResult));
                        }

                        conversationMessages.Add(new ConversationMessage("assistant", null, toolCalls));
                        notifyConversation();

                        requiresAction = true;
                        break;
                    }

                case ChatFinishReason.Length:
                    return Result<LlmCommandResult>.Failure("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    return Result<LlmCommandResult>.Failure("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    return Result<LlmCommandResult>.Failure("Deprecated in favor of tool calls.");

                default:
                    return Result<LlmCommandResult>.Failure($"Unhandled finish reason: {completion.FinishReason}");
            }
        } while (requiresAction);

        return Result<LlmCommandResult>.Failure("Unexpected error processing command - this should never be reached.");
    }

    private async Task<string> DispatchToolCallAsync(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken)
    {
        try
        {
            var chatTool = toolRegistry.GetTool(toolCall.FunctionName);
            return await chatTool.Execute(toolCall, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tool {ToolName} failed: {Error}", toolCall.FunctionName, ex.Message);

            var availableTools = string.Join(", ", toolRegistry.GetAllToolDefinitions().Select(t => t.FunctionName));
            return $"Error: {ex.Message} Available tools: {availableTools}.";
        }
    }

    private ChatCompletionOptions GetChatCompletionOptions()
    {
        var toolDefinitions = toolRegistry.GetAllToolDefinitions();
        var options = new ChatCompletionOptions();

        foreach (var toolDefinition in toolDefinitions)
        {
            options.Tools.Add(toolDefinition);
        }

        return options;
    }
}
