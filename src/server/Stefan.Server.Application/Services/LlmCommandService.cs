using OpenAI.Chat;
using Stefan.Server.Application.AI.Tools.Timer;
using Stefan.Server.Common;

namespace Stefan.Server.Application.Services;

public class LlmCommandService(
    ChatClient chatClient,
    TimerDbContext dbContext,
    ITimerScheduler timerScheduler)
{
    private static readonly ChatCompletionOptions CompletionOptions = new()
    {
        Tools = { AddTimerTool.Definition, ListTimersTool.Definition, CancelTimerTool.Definition },
    };

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

    public async Task<LlmCommandResult> ProcessCommandAsync(string command, string deviceId, CancellationToken cancellationToken = default)
    {
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

        bool requiresAction;

        do
        {
            requiresAction = false;
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, CompletionOptions, cancellationToken);

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                {
                    messages.Add(new AssistantChatMessage(completion));
                    var assistantMessage = completion.Content[0].Text;
                    ConsoleLog.Write(LogCategory.LLM, $"Assistant response: {assistantMessage}");
                    conversationMessages.Add(new ConversationMessage("assistant", assistantMessage, null));
                    return new LlmCommandResult(assistantMessage, conversationMessages);
                }

                case ChatFinishReason.ToolCalls:
                {
                    messages.Add(new AssistantChatMessage(completion));

                    var toolCalls = new List<ToolCallRecord>();

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        ConsoleLog.Write(LogCategory.LLM, $"Tool call: {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}");
                        var toolResult = await DispatchToolCallAsync(toolCall, deviceId, cancellationToken);
                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));

                        toolCalls.Add(new ToolCallRecord(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments.ToString(), toolResult));
                        conversationMessages.Add(new ConversationMessage("tool", toolResult, null));
                    }

                    conversationMessages.Add(new ConversationMessage("assistant", null, toolCalls));

                    requiresAction = true;
                    break;
                }

                case ChatFinishReason.Length:
                    throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new NotImplementedException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    throw new NotImplementedException(completion.FinishReason.ToString());
            }
        } while (requiresAction);

        return new LlmCommandResult("Error", conversationMessages);
    }

    private async Task<string> DispatchToolCallAsync(ChatToolCall toolCall, string deviceId, CancellationToken cancellationToken)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(AddTimerTool):
                return await new AddTimerTool(dbContext, timerScheduler).ExecuteAsync(toolCall, deviceId, cancellationToken);

            case nameof(ListTimersTool):
                return ListTimersTool.Execute(toolCall, dbContext);

            case nameof(CancelTimerTool):
                return await new CancelTimerTool(dbContext, timerScheduler).ExecuteAsync(toolCall, cancellationToken);

            default:
                throw new NotImplementedException($"Unknown tool call: {toolCall.FunctionName}");
        }
    }
}
