using System.Diagnostics;
using Vosk;

var builder = WebApplication.CreateBuilder(args);

var model = new Model("vosk-model-full");

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
    Console.WriteLine($"Received file: {file.FileName}, size: {file.Length} bytes");
    using var fileStream = file.OpenReadStream();
    string result = GetTextFromCommandAudioFile(fileStream, model);
    Console.WriteLine($"Transcription result: {result}");
    
    var ms = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
    Console.WriteLine($"Processing time: {ms} ms");
    return "OK";
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

    var finalResult = recognizer.FinalResult();
    Console.WriteLine(finalResult);
    return finalResult;
}