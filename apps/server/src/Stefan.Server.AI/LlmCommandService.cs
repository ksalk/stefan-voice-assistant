using OpenAI.Chat;
using Stefan.Server.AI.Tools.Timer;
using Stefan.Server.Common;

namespace Stefan.Server.AI;

public class LlmCommandService(ChatClient chatClient, TimerDbContext dbContext)
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
        Respond with simple plain confirmation message, ready to be TTS'd, no need for markdown or formatting.
        """;

    public string ProcessCommand(string command, string deviceId)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(command),
        ];

        bool requiresAction;

        do
        {
            requiresAction = false;
            ChatCompletion completion = chatClient.CompleteChat(messages, CompletionOptions);

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                {
                    messages.Add(new AssistantChatMessage(completion));
                    var assistantMessage = completion.Content[0].Text;
                    ConsoleLog.Write(LogCategory.LLM, $"Assistant response: {assistantMessage}");
                    return assistantMessage;
                }

                case ChatFinishReason.ToolCalls:
                {
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        ConsoleLog.Write(LogCategory.LLM, $"Tool call: {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}");
                        var toolResult = DispatchToolCall(toolCall, deviceId);
                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    }

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

        return "Error";
    }

    private string DispatchToolCall(ChatToolCall toolCall, string deviceId)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(AddTimerTool):
                return AddTimerTool.Execute(toolCall, dbContext, deviceId);

            case nameof(ListTimersTool):
                return ListTimersTool.Execute(toolCall, dbContext);

            case nameof(CancelTimerTool):
                return CancelTimerTool.Execute(toolCall, dbContext);

            default:
                throw new NotImplementedException($"Unknown tool call: {toolCall.FunctionName}");
        }
    }
}
