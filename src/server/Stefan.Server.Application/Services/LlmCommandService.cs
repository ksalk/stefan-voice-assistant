using System.Diagnostics;
using OpenAI.Chat;
using Stefan.Server.Application.Tools;
using Stefan.Server.Common;

namespace Stefan.Server.Application.Services;

public class LlmCommandService(
    ChatClient chatClient,
    ToolRegistry toolRegistry) : ILlmCommandService
{
    private static string BuildSystemPrompt() => $"""
        You are Stefan, a voice home assistant that manages timers using the provided tools.

        Rules:
        - Always use tools to create, list, and cancel timers — never track state yourself.
        - To cancel a timer by name, first call ListTimersTool to find its ID, then call CancelTimerTool.
        - If the user asks to set a timer but does not specify a duration, ask them how long.
        - You have access to the current time. You can convert absolute times (e.g. "6pm", "in 20 minutes") to seconds from now.

        Response format (critical — this is spoken aloud via TTS):
        - One short, natural sentence. No lists, markdown, symbols, or abbreviations.
        - Confirm the exact duration in human-friendly terms (e.g. "5 minutes", not "300 seconds").
        - Examples: "Sure, 5 minute timer started." / "You have two active timers." / "Your pasta timer has been cancelled."

        The current date and time is {DateTime.Now:dddd, MMMM d, yyyy h:mm tt}.
        """;

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
        bool requiresAction;

        do
        {
            ConsoleLog.Write(LogCategory.LLM, $"[llm] Sending command to LLM (message count: {messages.Count})...");
            requiresAction = false;
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, GetChatCompletionOptions(), cancellationToken);

            ConsoleLog.Write(LogCategory.LLM, $"[llm] Received response from LLM (finish reason: {completion.FinishReason})");

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        messages.Add(new AssistantChatMessage(completion));
                        var assistantMessage = completion.Content[0].Text;
                        ConsoleLog.Write(LogCategory.LLM, $"Assistant response: {assistantMessage}");
                        conversationMessages.Add(new ConversationMessage("assistant", assistantMessage, null));
                        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                        return new LlmCommandResult(assistantMessage, conversationMessages, durationMs);
                    }

                case ChatFinishReason.ToolCalls:
                    {
                        ConsoleLog.Write(LogCategory.LLM, $"LLM requested tool calls: {string.Join(", ", completion.ToolCalls.Select(c => c.FunctionName))}");
                        messages.Add(new AssistantChatMessage(completion));

                        var toolCalls = new List<ToolCallRecord>();

                        foreach (ChatToolCall toolCall in completion.ToolCalls)
                        {
                            ConsoleLog.Write(LogCategory.LLM, $"Tool call: {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}");
                            var toolResult = await DispatchToolCallAsync(toolCall, toolCallContext, cancellationToken);
                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));

                            toolCalls.Add(new ToolCallRecord(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments.ToString(), toolResult));
                            conversationMessages.Add(new ConversationMessage("tool", toolResult, null));
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
        var chatTool = toolRegistry.GetTool(toolCall.FunctionName) ?? throw new NotImplementedException($"Unknown tool call: {toolCall.FunctionName}");
        var toolResult = await chatTool.Execute(toolCall, context, cancellationToken);

        return toolResult;
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
