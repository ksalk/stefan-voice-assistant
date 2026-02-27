using System.Text.Json;
using Vosk;

namespace StefanAssistant.Server.API;

public class SpeechToTextService(Model model)
{
    public string Transcribe(Stream audioStream)
    {
        var recognizer = new VoskRecognizer(model, 16000.0f);
        recognizer.SetMaxAlternatives(0);
        recognizer.SetWords(true);

        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = audioStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            recognizer.AcceptWaveform(buffer, bytesRead);
        }

        var finalResultJson = recognizer.FinalResult();
        using var doc = JsonDocument.Parse(finalResultJson);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
