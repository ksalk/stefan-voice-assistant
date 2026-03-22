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

    private const string SystemPrompt = """
        You are a helpful assistant for managing timers. 
        You can respond to user requests to set timers and use the provided tool to create timers. 
        If the user asks you to set a timer, you should call the tool with the appropriate arguments. 
        Always use the tool to manage timers instead of trying to keep track of them yourself.
        Respond with simple plain confirmation message, one short sentence is best - ready to be TTS'd, no need for markdown or formatting.
        """;

    public async Task<LlmCommandResult> ProcessCommandAsync(string command, string deviceId, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(command),
        ];

        var conversationMessages = new List<ConversationMessage>
        {
            new("system", SystemPrompt, null),
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
