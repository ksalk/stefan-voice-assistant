using Microsoft.Extensions.Options;
using NanoWakeWord;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

/// <summary>
/// Wake-word detection with NanoWakeWord, a C# port of openWakeWord. Stop-keyword detection
/// stays on sherpa-onnx and is handled separately by the command dispatcher.
/// </summary>
public sealed class NanoWakeWordDetector : IWakeWordDetector
{
    private const int RequiredSampleRate = 16_000;

    /// <summary>Models embedded in NanoWakeWord. The library extracts them next to the app binary.</summary>
    private static readonly string[] LibraryModelFiles =
    [
        "melspectrogram.onnx",
        "embedding_model.onnx",
    ];

    private readonly NanoWakeWordOptions _options;
    private readonly ILogger<NanoWakeWordDetector> _logger;
    private WakeWordRuntime? _runtime;
    private bool _disposed;

    public NanoWakeWordDetector(
        IOptions<NanoWakeWordOptions> options,
        ILogger<NanoWakeWordDetector> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("NanoWakeWord:Model must be set.");
        }

        if (_options.StepFrames <= 0)
        {
            throw new InvalidOperationException("NanoWakeWord:StepFrames must be greater than zero.");
        }

        _runtime = CreateRuntime();

        _logger.LogInformation(
            "[wake] NanoWakeWord ready. Model: {Model}, threshold: {Threshold}, trigger level: {TriggerLevel}, refractory: {Refractory}, step frames: {StepFrames}",
            _options.Model,
            _options.Threshold,
            _options.TriggerLevel,
            _options.Refractory,
            _options.StepFrames);
    }

    public string? Process(RawPcmChunk chunk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var monoSamples = AudioProcessing.ConvertToMono(chunk);
        if (monoSamples.SampleRate != RequiredSampleRate)
        {
            monoSamples = AudioProcessing.Resample(monoSamples, RequiredSampleRate);
        }

        if (monoSamples.Samples.Length == 0)
        {
            return null;
        }

        var runtime = _runtime ??= CreateRuntime();
        var detectedIndex = runtime.Process(monoSamples.Samples);
        if (detectedIndex < 0)
        {
            return null;
        }

        // NanoWakeWord has no reset API. Drop the runtime so the next wake-word session starts
        // with empty sample, mel, embedding, activation and refractory state. Reloading is
        // deferred to the next Process call, which happens after the command session closes.
        DisposeRuntime();

        return detectedIndex == 0 ? _options.Keyword : null;
    }

    public void Reset()
    {
        if (_disposed)
        {
            return;
        }

        // Recreate eagerly. Callers such as the benchmark reset before timing a clip, so the
        // model load must not be attributed to inference time.
        DisposeRuntime();
        _runtime = CreateRuntime();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeRuntime();
    }

    private void DisposeRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
    }

    private WakeWordRuntime CreateRuntime()
    {
        EnsureModelFiles();

        return new WakeWordRuntime(new WakeWordRuntimeConfig
        {
            StepFrames = _options.StepFrames,
            WakeWords =
            [
                new WakeWordConfig
                {
                    Model = _options.Model,
                    Threshold = _options.Threshold,
                    TriggerLevel = _options.TriggerLevel,
                    Refractory = _options.Refractory,
                }
            ],
        });
    }

    /// <summary>
    /// NanoWakeWord loads <c>models/&lt;Model&gt;.onnx</c> relative to the current working
    /// directory. It extracts its embedded mel and embedding models next to the app binary.
    /// Copy the required models into the working directory when the two locations differ.
    /// </summary>
    private void EnsureModelFiles()
    {
        typeof(WakeWordRuntime).Assembly.ExtractModels();

        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "models");
        var targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), "models");

        foreach (var fileName in LibraryModelFiles.Append($"{_options.Model}.onnx"))
        {
            CopyIfNewer(Path.Combine(sourceDirectory, fileName), Path.Combine(targetDirectory, fileName));
        }
    }

    private static void CopyIfNewer(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"NanoWakeWord model file '{sourcePath}' was not found. " +
                "Build the node so the model is copied to the output models directory.",
                sourcePath);
        }

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (!File.Exists(destinationPath) ||
            File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destinationPath))
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }
}
