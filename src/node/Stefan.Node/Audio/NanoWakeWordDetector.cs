using Microsoft.Extensions.Options;
using Stefan.Node.Audio.NanoWakeWord;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

/// <summary>
/// Wake-word detection with NanoWakeWord, a C# port of openWakeWord. Stop-keyword detection
/// stays on sherpa-onnx and is handled separately by the command dispatcher.
/// </summary>
public sealed class NanoWakeWordDetector : IWakeWordDetector
{
    private const int RequiredSampleRate = 16_000;

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

        if (_options.IntraOpThreads <= 0 || _options.InterOpThreads <= 0)
        {
            throw new InvalidOperationException("NanoWakeWord thread counts must be greater than zero.");
        }

        _runtime = CreateRuntime();

        _logger.LogInformation(
            "[wake] NanoWakeWord ready. Model: {Model}, threshold: {Threshold}, trigger level: {TriggerLevel}, refractory: {Refractory}, step frames: {StepFrames}, intra-op threads: {IntraOpThreads}, inter-op threads: {InterOpThreads}, spinning: {AllowSpinning}",
            _options.Model,
            _options.Threshold,
            _options.TriggerLevel,
            _options.Refractory,
            _options.StepFrames,
            _options.IntraOpThreads,
            _options.InterOpThreads,
            _options.AllowSpinning);
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

        // Clear the streaming state so the next wake-word session starts with empty sample,
        // mel, embedding, activation and refractory state. The ONNX sessions stay loaded.
        runtime.Reset();

        return detectedIndex == 0 ? _options.Keyword : null;
    }

    public void Reset()
    {
        if (_disposed)
        {
            return;
        }

        // Callers such as the benchmark reset before timing a clip. The runtime is already
        // loaded, so this only clears the streaming state.
        _runtime ??= CreateRuntime();
        _runtime.Reset();
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
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "models");

        return new WakeWordRuntime(new WakeWordRuntimeConfig
        {
            StepFrames = _options.StepFrames,
            IntraOpThreads = _options.IntraOpThreads,
            InterOpThreads = _options.InterOpThreads,
            AllowSpinning = _options.AllowSpinning,
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
        }, modelDirectory);
    }
}
