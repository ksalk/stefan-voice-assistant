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
        You are Stefan, a voice home assistant that manages timers using the provided tools.

        Rules:
        - Always use tools to create, list, and cancel timers — never track state yourself.
        - To cancel a timer by name, first call list_timers to find its ID, then call cancel_timer.
        - You have access to the current time. You can convert absolute times (e.g. "6pm", "in 20 minutes") to seconds from now.

        Response format (critical — this is spoken aloud via TTS):
        - One short, natural sentence. No lists, markdown, symbols, or abbreviations.
        - Confirm the exact duration in human-friendly terms (e.g. "5 minutes", not "300 seconds").
        - Examples: "Sure, 5 minute timer started." / "You have two active timers." / "Your pasta timer has been cancelled."

        The current date and time is {DateTime.Now:dddd, MMMM d, yyyy h:mm tt}.
        """;

    private const int MaxToolCallIterations = 5;

    // TODO: remove async from name
    public async Task<Result<LlmCommandResult>> ProcessCommandAsync(string command, string deviceId, CancellationToken cancellationToken = default)
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

        var toolCallContext = new ToolCallContext()
        {
            SourceDeviceId = deviceId
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
                        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                       
                        return new LlmCommandResult(assistantMessage, conversationMessages, durationMs);
                    }

                case ChatFinishReason.ToolCalls:
                    {
                        logger.LogInformation("LLM requested tool calls: {ToolCalls}", string.Join(", ", completion.ToolCalls.Select(c => c.FunctionName)));

                        if (++toolCallIterations > MaxToolCallIterations)
                        {
                            logger.LogWarning("Tool call limit of {MaxToolCallIterations} exceeded, aborting command", MaxToolCallIterations);
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
