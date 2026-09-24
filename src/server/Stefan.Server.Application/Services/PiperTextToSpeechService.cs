using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PiperSharp;
using PiperSharp.Models;

namespace Stefan.Server.Application.Services;

public class PiperTextToSpeechService : ITextToSpeechService
{
    private readonly PiperProvider _piper;

    public PiperTextToSpeechService(IConfiguration configuration, ILogger<PiperTextToSpeechService> logger)
    {
        var section = configuration.GetSection("Piper");
        var executablePath = section["ExecutablePath"] ?? "piper/piper";
        var workingDirectory = section["WorkingDirectory"] ?? "piper";
        var modelKey = section["ModelKey"] ?? "en_US-hfc_female-medium";

        logger.LogInformation("Initializing Piper TTS...");

        // Ensure piper executable exists, download if missing
        var fullExePath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullExePath))
        {
            logger.LogInformation("Piper executable not found at {ExecutablePath}, downloading...", fullExePath);
            var cwd = Path.GetFullPath(workingDirectory);
            var parentDir = Directory.GetParent(cwd)?.FullName ?? cwd;
            PiperDownloader.DownloadPiper().ExtractPiper(parentDir).GetAwaiter().GetResult();
            logger.LogInformation("Piper executable downloaded and extracted.");
        }

        // Load or download the voice model
        VoiceModel model;
        try
        {
            logger.LogInformation("Loading voice model {ModelKey}...", modelKey);
            model = VoiceModel.LoadModelByKey(modelKey).GetAwaiter().GetResult();
            logger.LogInformation("Voice model loaded from disk.");
        }
        catch
        {
            logger.LogInformation("Voice model {ModelKey} not found locally, downloading...", modelKey);
            model = PiperDownloader.DownloadModelByKey(modelKey).GetAwaiter().GetResult();
            logger.LogInformation("Voice model downloaded.");
        }

        _piper = new PiperProvider(new PiperConfiguration
        {
            ExecutableLocation = Path.GetFullPath(executablePath),
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            Model = model,
        });

        logger.LogInformation("Piper TTS initialized successfully.");
    }

    /// <summary>
    /// Synthesize the given text to a WAV byte array.
    /// </summary>
    public async Task<Result<TextToSpeechResult>> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timestamp = Stopwatch.GetTimestamp();
            var audioBytes = await _piper.InferAsync(text, AudioOutputType.Wav);
            var durationMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            return Result<TextToSpeechResult>.Success(new TextToSpeechResult(audioBytes, durationMs));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TextToSpeechResult>.Failure(ex.Message);
        }
    }
}
