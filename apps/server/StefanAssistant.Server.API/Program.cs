using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using StefanAssistant.Server.Tools.Timer;
using Vosk;

var builder = WebApplication.CreateBuilder(args);

var model = new Model("vosk-model-full");

var configuration = builder.Configuration;
string openAIApiKey = configuration["OpenAI:ApiKey"];

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapPost("/command", async (IFormFile file) =>
{
    var timestamp = Stopwatch.GetTimestamp();
    Console.WriteLine($"***************************************************************");
    Console.WriteLine($"[HTTP] Received file: {file.FileName}, size: {file.Length} bytes");
    using var fileStream = file.OpenReadStream();
    string transcriptionResult = GetTextFromCommandAudioFile(fileStream, model);
    Console.WriteLine($"[STT] Transcription result: {transcriptionResult}");

    var ms = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
    Console.WriteLine($"[STT] Speech processing time: {ms} ms");

    timestamp = Stopwatch.GetTimestamp();
    string assistantResponse = ProcessCommandUsingLLM(transcriptionResult);
    Console.WriteLine($"[LLM] Assistant response: {assistantResponse}");
    ms = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
    Console.WriteLine($"[LLM] LLM processing time: {ms} ms");

    return assistantResponse;
})
.DisableAntiforgery() // TODO: fix in future for security
.WithName("ProcessCommand");

app.Run();

string GetTextFromCommandAudioFile(Stream fileStream, Model model)
{
    VoskRecognizer recognizer = new VoskRecognizer(model, 16000.0f);
    recognizer.SetMaxAlternatives(0);
    recognizer.SetWords(true);

    byte[] buffer = new byte[4096];
    int bytesRead;
    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        recognizer.AcceptWaveform(buffer, bytesRead);
    }

    var finalResultJson = recognizer.FinalResult();
    var finalResultObj = JsonDocument.Parse(finalResultJson);
    var finalResult = finalResultObj.RootElement.GetProperty("text").GetString();

    return finalResult;
}

string ProcessCommandUsingLLM(string command)
{
    ChatTool addTimerTool = ChatTool.CreateFunctionTool(
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

    ChatClient client = new(
        model: "openai/gpt-oss-120b",
        credential: new ApiKeyCredential(openAIApiKey),
        options: new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1")
        });

    var systemPrompt = """
        You are a helpful assistant for managing timers. 
        You can respond to user requests to set timers and use the provided tool to create timers. 
        If the user asks you to set a timer, you should call the tool with the appropriate arguments. 
        Always use the tool to manage timers instead of trying to keep track of them yourself.
        Respond with simple plain confirmation message.
        """;
    
    List<ChatMessage> messages =
    [
        new SystemChatMessage(systemPrompt),
        new UserChatMessage(command),
    ];

    ChatCompletionOptions options = new()
    {
        Tools = { addTimerTool },
    };

    bool requiresAction;

    do
    {
        requiresAction = false;
        ChatCompletion completion = client.CompleteChat(messages, options);

        switch (completion.FinishReason)
        {
            case ChatFinishReason.Stop:
            {
                // Add the assistant message to the conversation history.
                messages.Add(new AssistantChatMessage(completion));
                var assistantMessage = completion.Content[0].Text;
                Console.WriteLine($"Assistant response: {assistantMessage}");
                return assistantMessage;
                break;
            }

            case ChatFinishReason.ToolCalls:
            {
                // First, add the assistant message with tool calls to the conversation history.
                messages.Add(new AssistantChatMessage(completion));
                var assistantMessage = completion.Content[0].Text;

                // Then, add a new tool message for each tool call that is resolved.
                foreach (ChatToolCall toolCall in completion.ToolCalls)
                {
                    Console.WriteLine($"Tool call: {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}");
                    switch (toolCall.FunctionName)
                    {
                        case nameof(TimerTools.AddTimer):
                            {
                                // The arguments that the model wants to use to call the function are specified as a
                                // stringified JSON object based on the schema defined in the tool definition. Note that
                                // the model may hallucinate arguments too. Consequently, it is important to do the
                                // appropriate parsing and validation before calling the function.
                                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                bool hasSeconds = argumentsJson.RootElement.TryGetProperty("seconds", out JsonElement seconds);
                                bool hasLabel = argumentsJson.RootElement.TryGetProperty("label", out JsonElement label);

                                if (!hasSeconds)
                                {
                                    throw new ArgumentNullException(nameof(seconds), "The seconds argument is required.");
                                }

                                string toolResult = hasLabel
                                    ? TimerTools.AddTimer(seconds.GetInt32(), label.GetString())
                                    : TimerTools.AddTimer(seconds.GetInt32(), null);
                                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                break;
                            }

                        default:
                            {
                                // Handle other unexpected calls.
                                throw new NotImplementedException();
                            }
                    }
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

