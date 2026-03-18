using Microsoft.Extensions.Configuration;
using PiperSharp;
using PiperSharp.Models;
using Stefan.Server.Common;

namespace Stefan.Server.Application.Services;

public class TextToSpeechService
{
    private readonly PiperProvider _piper;

    public TextToSpeechService(IConfiguration configuration)
    {
        var section = configuration.GetSection("Piper");
        var executablePath = section["ExecutablePath"] ?? "piper/piper";
        var workingDirectory = section["WorkingDirectory"] ?? "piper";
        var modelKey = section["ModelKey"] ?? "en_US-hfc_female-medium";

        ConsoleLog.Write(LogCategory.TTS, "Initializing Piper TTS...");

        // Ensure piper executable exists, download if missing
        var fullExePath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullExePath))
        {
            ConsoleLog.Write(LogCategory.TTS, $"Piper executable not found at '{fullExePath}', downloading...");
            var cwd = Path.GetFullPath(workingDirectory);
            var parentDir = Directory.GetParent(cwd)?.FullName ?? cwd;
            PiperDownloader.DownloadPiper().ExtractPiper(parentDir).GetAwaiter().GetResult();
            ConsoleLog.Write(LogCategory.TTS, "Piper executable downloaded and extracted.");
        }

        // Load or download the voice model
        VoiceModel model;
        try
        {
            ConsoleLog.Write(LogCategory.TTS, $"Loading voice model '{modelKey}'...");
            model = VoiceModel.LoadModelByKey(modelKey).GetAwaiter().GetResult();
            ConsoleLog.Write(LogCategory.TTS, "Voice model loaded from disk.");
        }
        catch
        {
            ConsoleLog.Write(LogCategory.TTS, $"Voice model '{modelKey}' not found locally, downloading...");
            model = PiperDownloader.DownloadModelByKey(modelKey).GetAwaiter().GetResult();
            ConsoleLog.Write(LogCategory.TTS, "Voice model downloaded.");
        }

        _piper = new PiperProvider(new PiperConfiguration
        {
            ExecutableLocation = Path.GetFullPath(executablePath),
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            Model = model,
        });

        ConsoleLog.Write(LogCategory.TTS, "Piper TTS initialized successfully.");
    }

    /// <summary>
    /// Synthesize the given text to a WAV byte array.
    /// </summary>
    public async Task<byte[]> SynthesizeAsync(string text)
    {
        return await _piper.InferAsync(text, AudioOutputType.Wav);
    }
}
