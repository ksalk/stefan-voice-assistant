using System.ClientModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using StefanAssistant.Server.AI;
using StefanAssistant.Server.AI.Tools.Timer;
using StefanAssistant.Server.API;
using StefanAssistant.Server.Common;
using Whisper.net;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ =>
{
    var factory = WhisperFactory.FromPath("ggml-base.bin");
    return factory.CreateBuilder()
        .WithLanguage("en")
        .Build();
});
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
builder.Services.AddDbContext<TimerDbContext>(o =>
    o.UseSqlite(configuration.GetConnectionString("TimerDb")));
builder.Services.AddScoped<LlmCommandService>();
builder.Services.AddSingleton<NodeRegistry>();
builder.Services.AddSingleton<NodeWebSocketHandler>();

var app = builder.Build();

app.UseWebSockets();

// Ensure the SQLite database and schema exist on startup.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<TimerDbContext>().Database.EnsureCreated();

// Eagerly load the STT model so it's ready before the first request.
app.Services.GetRequiredService<SpeechToTextService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/command", async (IFormFile file, SpeechToTextService stt, LlmCommandService llm) =>
{
    var timestamp = Stopwatch.GetTimestamp();
    ConsoleLog.WriteSeparator();
    ConsoleLog.Write(LogCategory.HTTP, $"Received file: {file.FileName}, size: {file.Length} bytes");

    using var fileStream = file.OpenReadStream();

    string transcript = await stt.TranscribeAsync(fileStream);
    ConsoleLog.Write(LogCategory.STT, $"Transcription result: {transcript}");
    ConsoleLog.Write(LogCategory.STT, $"Speech processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    timestamp = Stopwatch.GetTimestamp();
    string response = llm.ProcessCommand(transcript);

    ConsoleLog.Write(LogCategory.LLM, $"LLM processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    return response;
})
.DisableAntiforgery() // TODO: fix in future for security
.WithName("ProcessCommand");

app.Map("/ws", (HttpContext context, NodeWebSocketHandler wsHandler, CancellationToken cancellationToken) =>
    wsHandler.HandleAsync(context, cancellationToken));


app.Run();
