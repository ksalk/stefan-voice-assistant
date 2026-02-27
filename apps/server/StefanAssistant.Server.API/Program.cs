using System.ClientModel;
using System.Diagnostics;
using OpenAI;
using OpenAI.Chat;
using StefanAssistant.Server.API;
using Vosk;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ => new Model("vosk-model-full"));
builder.Services.AddSingleton(_ =>
{
    string apiKey = configuration["OpenAI:ApiKey"]!;
    string model = configuration["OpenAI:Model"]!;
    string endpoint = configuration["OpenAI:Endpoint"]!;

    return new ChatClient(
        model: model,
        credential: new ApiKeyCredential(apiKey),
        options: new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
});
builder.Services.AddSingleton<SpeechToTextService>();
builder.Services.AddSingleton<LlmCommandService>();

var app = builder.Build();

// Eagerly load the STT model so it's ready before the first request.
app.Services.GetRequiredService<SpeechToTextService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/command", (IFormFile file, SpeechToTextService stt, LlmCommandService llm) =>
{
    var timestamp = Stopwatch.GetTimestamp();
    Console.WriteLine($"***************************************************************");
    Console.WriteLine($"[HTTP] Received file: {file.FileName}, size: {file.Length} bytes");

    using var fileStream = file.OpenReadStream();
    string transcript = stt.Transcribe(fileStream);
    Console.WriteLine($"[STT] Transcription result: {transcript}");
    Console.WriteLine($"[STT] Speech processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    timestamp = Stopwatch.GetTimestamp();
    string response = llm.ProcessCommand(transcript);
    Console.WriteLine($"[LLM] LLM processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    return response;
})
.DisableAntiforgery() // TODO: fix in future for security
.WithName("ProcessCommand");

app.Run();
