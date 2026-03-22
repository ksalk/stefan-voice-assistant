using System.Text.Json;
using Vosk;

namespace Stefan.Server.Application.Services;

public class VoskSpeechToTextService : ISpeechToTextService, IDisposable
{
    private const int SampleRate = 16000;

    private readonly Model _model;
    private readonly VoskRecognizer _recognizer;

    public VoskSpeechToTextService(string modelPath)
    {
        Vosk.Vosk.SetLogLevel(0);
        _model = new Model(modelPath);
        _recognizer = new VoskRecognizer(_model, SampleRate);
        _recognizer.SetMaxAlternatives(0);
        _recognizer.SetWords(true);
    }

    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = await audioStream.ReadAsync(buffer)) > 0)
        {
            _recognizer.AcceptWaveform(buffer, bytesRead);
        }

        var resultJson = _recognizer.FinalResult();
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        return result.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    public void Dispose()
    {
        _recognizer.Dispose();
        _model.Dispose();
    }
}
