using System.Text.Json;
using OpenAI.Chat;
using StefanAssistant.Server.Tools.Timer;

namespace StefanAssistant.Server.API;

public class LlmCommandService(ChatClient chatClient)
{
    private static readonly ChatTool AddTimerTool = ChatTool.CreateFunctionTool(
        functionName: nameof(TimerTools.AddTimer),
        functionDescription: "Add a timer",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "seconds": {
                    "type": "integer",
                    "description": "Number of seconds for the timer."
                },
                "label": {
                    "type": "string",
                    "description": "A label for the timer if desired."
                }
            },
            "required": [ "seconds" ]
        }
        """u8.ToArray())
    );

    private static readonly ChatCompletionOptions CompletionOptions = new()
    {
        Tools = { AddTimerTool },
    };

    private const string SystemPrompt = """
        You are a helpful assistant for managing timers. 
        You can respond to user requests to set timers and use the provided tool to create timers. 
        If the user asks you to set a timer, you should call the tool with the appropriate arguments. 
        Always use the tool to manage timers instead of trying to keep track of them yourself.
        Respond with simple plain confirmation message.
        """;

    public string ProcessCommand(string command)
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
                    Console.WriteLine($"[LLM] Assistant response: {assistantMessage}");
                    return assistantMessage;
                }

                case ChatFinishReason.ToolCalls:
                {
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (ChatToolCall toolCall in completion.ToolCalls)
                    {
                        Console.WriteLine($"[LLM] Tool call: {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}");
                        var toolResult = DispatchToolCall(toolCall);
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

    private static string DispatchToolCall(ChatToolCall toolCall)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(TimerTools.AddTimer):
            {
                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                bool hasSeconds = argumentsJson.RootElement.TryGetProperty("seconds", out JsonElement seconds);
                bool hasLabel = argumentsJson.RootElement.TryGetProperty("label", out JsonElement label);

                if (!hasSeconds)
                    throw new ArgumentNullException(nameof(seconds), "The seconds argument is required.");

                return hasLabel
                    ? TimerTools.AddTimer(seconds.GetInt32(), label.GetString())
                    : TimerTools.AddTimer(seconds.GetInt32(), null);
            }

            default:
                throw new NotImplementedException($"Unknown tool call: {toolCall.FunctionName}");
        }
    }
}
