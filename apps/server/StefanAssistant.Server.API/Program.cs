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
    // write out pwd
    Console.WriteLine(Directory.GetCurrentDirectory());
    Console.WriteLine($"Received file: {file.FileName}, size: {file.Length} bytes");
    using var fileStream = file.OpenReadStream();
    string result = GetTextFromCommandAudioFile(fileStream, model);
    Console.WriteLine($"Transcription result: {result}");
    
    var ms = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
    Console.WriteLine($"Processing time: {ms} ms");
    return "OK";
})
.DisableAntiforgery()
.WithName("ProcessCommand");

app.Run();

string GetTextFromCommandAudioFile(Stream fileStream, Model model)
{
    VoskRecognizer rec = new VoskRecognizer(model, 16000.0f);
    rec.SetMaxAlternatives(0);
    rec.SetWords(true);

    byte[] buffer = new byte[4096];
    int bytesRead;
    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        rec.AcceptWaveform(buffer, bytesRead);
        // if (rec.AcceptWaveform(buffer, bytesRead))
        // {
        //     Console.WriteLine(rec.Result());
        // }
        // else
        // {
        //     Console.WriteLine(rec.PartialResult());
        // }
    }

    Console.WriteLine(rec.FinalResult());
    return rec.FinalResult();
}