using System.ClientModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using StefanAssistant.Server.API;
using StefanAssistant.Server.Tools.Timer;
using Whisper.net;
using Whisper.net.Ggml;

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

var app = builder.Build();

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
    Console.WriteLine($"***************************************************************");
    Console.WriteLine($"[HTTP] Received file: {file.FileName}, size: {file.Length} bytes");

    using var fileStream = file.OpenReadStream();
    
    string transcript = await stt.TranscribeAsync(fileStream);
    Console.WriteLine($"[STT] Transcription result: {transcript}");
    Console.WriteLine($"[STT] Speech processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    timestamp = Stopwatch.GetTimestamp();
    string response = llm.ProcessCommand(transcript);

    // using var memoryStream = new MemoryStream();
    // await file.CopyToAsync(memoryStream);
    // byte[] audioBytes = memoryStream.ToArray();

    // string response = llm.ProcessAudioCommand(audioBytes);

    Console.WriteLine($"[LLM] LLM processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

    return response;
})
.DisableAntiforgery() // TODO: fix in future for security
.WithName("ProcessCommand");

app.Run();
