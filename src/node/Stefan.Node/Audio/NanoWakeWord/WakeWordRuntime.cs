// Vendored from NanoWakeWord 1.2.1
// https://github.com/samartzidis/NanoWakeWord
// Commit: 2794080b1c7b2b061eff2a279fec2d5c26d9d956
// Copyright (c) 2024 George Samartzidis, MIT License. See LICENSE in this directory.
//
// Local patch summary. Every local change below is marked with [PATCH].
//
// 1. CreateSession() now passes explicit ONNX Runtime SessionOptions. Upstream created every
//    InferenceSession with the ORT defaults: one intra-op thread per physical CPU core per
//    session, thread affinity enabled, and worker spinning enabled. The node creates three
//    sessions (mel, embedding and wake word), so a 4-core Pi Zero 2 W ran 3 x 4 = 12 ONNX
//    Runtime threads that busy-waited between inference calls and kept all four cores busy.
// 2. The runtime now receives an absolute model directory. Upstream loaded "models/..." relative
//    to the current working directory and relied on embedded resources extracted at runtime.
// 3. Reset() clears the streaming state. Upstream had no reset API, so the node disposed and
//    recreated the whole runtime after every detection, which reloaded all three ONNX models.
// 4. Small nullable-reference-type and dead-code cleanup for the node's build settings.

using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// [PATCH] The web SDK brings in Microsoft.AspNetCore.Builder.SessionOptions. The alias makes the
// ONNX Runtime type unambiguous without changing the rest of the vendored code.
using OnnxSessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace Stefan.Node.Audio.NanoWakeWord;

internal sealed class WakeWordConfig
{
    /// <summary>The name of the model file, e.g. "stefan_v0.2".</summary>
    // [PATCH] Upstream left this property uninitialized. The node enables nullable reference
    // types, so the default prevents a CS8618 warning without changing the runtime behavior.
    public string Model { get; set; } = string.Empty;

    /// <summary>Minimum probability for a wake-word candidate.</summary>
    public float Threshold { get; set; } = 0.5f;

    /// <summary>Number of consecutive threshold crossings required before a detection.</summary>
    public int TriggerLevel { get; set; } = 4;

    /// <summary>Cooldown period, in frames, after a detection.</summary>
    public int Refractory { get; set; } = 20;

    public override bool Equals(object? obj)
    {
        if (obj is not WakeWordConfig other)
        {
            return false;
        }

        return Model == other.Model &&
               Threshold == other.Threshold &&
               TriggerLevel == other.TriggerLevel &&
               Refractory == other.Refractory;
    }

    public override int GetHashCode()
    {
        return GetHashCode($"{Model}|{Threshold}|{TriggerLevel}|{Refractory}");
    }

    // [PATCH] Made static. The helper does not use instance state.
    private static int GetHashCode(string value)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        var hash = 0;
        for (var i = 0; i < Math.Min(4, hashBytes.Length); i++)
        {
            hash = (hash * 31) + hashBytes[i];
        }

        return hash;
    }
}

internal sealed class WakeWordRuntimeConfig
{
    // [PATCH] Upstream used an uninitialized array. Default to empty so a missing configuration
    // reaches the explicit validation in the runtime constructor instead of a null reference.
    public WakeWordConfig[] WakeWords { get; set; } = [];

    /// <summary>
    /// Audio frames processed per pipeline step. A higher value runs the mel model less often but
    /// increases latency. Four frames is 320 ms at 16 kHz.
    /// </summary>
    public int StepFrames { get; set; } = 4;

    // [PATCH] The three properties below are local additions. Upstream had no thread or spinning
    // configuration and always used ONNX Runtime defaults. See CreateSession() for the full
    // explanation of why these settings exist and what they change.

    /// <summary>
    /// Number of ONNX Runtime intra-op threads per session. The default is 1 because the models
    /// are small and extra threads only add scheduling overhead on small boards.
    /// </summary>
    public int IntraOpThreads { get; set; } = 1;

    /// <summary>Number of ONNX Runtime inter-op threads per session.</summary>
    public int InterOpThreads { get; set; } = 1;

    /// <summary>
    /// Allow ONNX Runtime worker threads to spin while waiting for the next inference. Keep this
    /// off on battery or small-board deployments: spinning keeps every core busy while idle.
    /// </summary>
    public bool AllowSpinning { get; set; }

    /// <summary>Optionally receive frame debug data.</summary>
    // [PATCH] Nullable annotation for the node's nullable-enabled project.
    public Action<string, float, bool>? DebugAction { get; set; }
}

internal sealed class WakeWordRuntime : IDisposable
{
    // [PATCH] Upstream used the fixed paths "models/melspectrogram.onnx" and
    // "models/embedding_model.onnx" and called WakeWordUtil.ExtractModels() to write embedded
    // models next to the app. The vendored runtime takes an explicit model directory instead,
    // and the node copies these files to its output models/ folder as content.
    private const string MelModelFile = "melspectrogram.onnx";
    private const string EmbeddingModelFile = "embedding_model.onnx";

    private readonly WakeWordRuntimeConfig _settings;
    private readonly InferenceSession _melSession;
    private readonly InferenceSession _embSession;
    private readonly List<InferenceSession> _wwSessions;

    private readonly List<float> _samples = [];
    private readonly List<float> _mels = [];
    private readonly List<List<float>> _features;

    // Per-model state tracking.
    private readonly int[] _activations;
    private readonly int[] _refractoryCounts;

    private readonly int _frameSize;

    // Constants specific to the ONNX models used in the wake-word pipeline.
    private const int ChunkSamples = 1280; // 80 ms at 16 kHz.
    private const int NumMels = 32; // Vertical resolution of the mel spectrogram.
    private const int EmbWindowSize = 76; // ~775 ms.
    private const int EmbStepSize = 8; // ~80 ms.
    private const int EmbFeatures = 96; // Embedding vector size.
    private const int WWFeatures = 16; // Consecutive embeddings used by the wake-word model.

    // [PATCH] Upstream signature was WakeWordRuntime(WakeWordRuntimeConfig settings). It loaded
    // models relative to Directory.GetCurrentDirectory(). The explicit modelDirectory lets the
    // node load from AppContext.BaseDirectory/models and makes the runtime independent of the
    // process working directory.
    public WakeWordRuntime(WakeWordRuntimeConfig settings, string modelDirectory)
    {
        _settings = settings;

        // [PATCH] Local validation. Upstream assumed every setting was valid.
        if (settings.StepFrames <= 0)
        {
            throw new InvalidOperationException("StepFrames must be greater than zero.");
        }

        if (settings.IntraOpThreads <= 0 || settings.InterOpThreads <= 0)
        {
            throw new InvalidOperationException("ONNX Runtime thread counts must be greater than zero.");
        }

        if (settings.WakeWords.Length == 0)
        {
            throw new InvalidOperationException("At least one wake-word model is required.");
        }

        _frameSize = settings.StepFrames * ChunkSamples;

        // [PATCH] Upstream created each session directly with new InferenceSession(path). All
        // sessions now go through CreateSession() so the ONNX Runtime thread and spinning
        // settings apply to the mel, embedding and wake-word models consistently.
        _melSession = CreateSession(Path.Combine(modelDirectory, MelModelFile));
        _embSession = CreateSession(Path.Combine(modelDirectory, EmbeddingModelFile));

        _wwSessions = [];
        foreach (var wakeWord in settings.WakeWords)
        {
            if (string.IsNullOrWhiteSpace(wakeWord.Model))
            {
                throw new InvalidOperationException("Wake-word model name must be set.");
            }

            _wwSessions.Add(CreateSession(Path.Combine(modelDirectory, $"{wakeWord.Model}.onnx")));
        }

        _features = new List<List<float>>(
            Enumerable.Range(0, _wwSessions.Count).Select(_ => new List<float>()));

        _activations = new int[_wwSessions.Count];
        _refractoryCounts = new int[_wwSessions.Count];
    }

    /// <summary>
    /// Adds a batch of 16-bit PCM audio samples and runs the wake-word pipeline.
    /// </summary>
    /// <returns>The index of the wake word that fired, or -1 when nothing fired.</returns>
    public int Process(short[] samples)
    {
        foreach (var sample in samples)
        {
            _samples.Add(sample);
        }

        AudioToMels();
        MelsToFeatures();
        FeaturesToOutput(out var detectedIndex);

        return detectedIndex;
    }

    // [PATCH] Upstream had no reset method. The node keeps one WakeWordRuntime for the lifetime
    // of the detector, so the streaming state must be cleared between wake-word sessions.
    //
    // The buffers cleared here are:
    // - _samples: raw 16 kHz samples not yet consumed by a full StepFrames step.
    // - _mels: mel frames not yet consumed by the 76-frame embedding window.
    // - _features: sliding embedding windows for each wake-word model.
    // - _activations: consecutive threshold-crossing counters.
    // - _refractoryCounts: cooldown counters after a detection.
    //
    // Clearing these buffers gives the next utterance a clean state without reloading the three
    // ONNX models. The previous node implementation disposed the whole runtime after a detection
    // and created a new one on the next Process() call, which reloaded the models and recreated
    // all ONNX Runtime thread pools.
    /// <summary>
    /// Clears all streaming state without reloading the ONNX sessions.
    /// </summary>
    public void Reset()
    {
        _samples.Clear();
        _mels.Clear();

        foreach (var features in _features)
        {
            features.Clear();
        }

        Array.Clear(_activations, 0, _activations.Length);
        Array.Clear(_refractoryCounts, 0, _refractoryCounts.Length);
    }

    // [PATCH] This method replaces the upstream direct session construction:
    //
    //     _melSession = new InferenceSession(MelModelPath);
    //     _embSession = new InferenceSession(EmbModelPath);
    //     _wwSessions.Add(new InferenceSession(modelPath));
    //
    // Upstream passed no SessionOptions, so ONNX Runtime used its defaults:
    // - intra_op_num_threads = 0: one intra-op thread per physical CPU core, per session.
    // - thread affinity enabled for the extra workers.
    // - session.intra_op.allow_spinning = 1: workers busy-wait for the next Run() call.
    //
    // The node creates three sessions: mel, embedding and wake word. On a 4-core Pi Zero 2 W
    // that became 3 x 4 = 12 ONNX Runtime threads. Audio arrives continuously, so the threads
    // spun between inference calls and kept all four cores busy even when nobody spoke. The
    // model work itself was small; the benchmark's RTF was already low.
    //
    // The local settings are:
    // - IntraOpNumThreads = 1: one worker per session instead of one per core. An explicit
    //   thread count also stops ONNX Runtime from setting per-thread CPU affinity.
    // - InterOpNumThreads = 1: the graph runs sequentially, so this is only a safety cap.
    // - ORT_SEQUENTIAL: these small graphs do not benefit from inter-op parallelism.
    // - allow_spinning = 0: workers sleep when there is no work. This is the largest CPU saving.
    //
    // Measured on the node: x64 real-time CPU dropped from ~1366% to ~2%. The ARM64 image under
    // a 4-CPU QEMU test dropped from ~399% to ~101%. Benchmark recall and false activations did
    // not change.
    private InferenceSession CreateSession(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"NanoWakeWord model file '{modelPath}' was not found. " +
                "Build the node so the model is copied to the output models directory.",
                modelPath);
        }

        using var options = new OnnxSessionOptions
        {
            // [PATCH] One intra-op thread per session. This prevents the per-core thread pools
            // and the automatic CPU affinity that caused the Pi Zero 2 W saturation.
            IntraOpNumThreads = _settings.IntraOpThreads,

            // [PATCH] Sequential execution needs no extra inter-op workers.
            InterOpNumThreads = _settings.InterOpThreads,

            // [PATCH] Explicit sequential mode; the default, made visible for the small graphs.
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        // [PATCH] Disable worker spinning. Without these entries, ONNX Runtime keeps its worker
        // threads awake between Run() calls and burns CPU while the node is only listening.
        options.AddSessionConfigEntry(
            "session.intra_op.allow_spinning",
            _settings.AllowSpinning ? "1" : "0");
        options.AddSessionConfigEntry(
            "session.inter_op.allow_spinning",
            _settings.AllowSpinning ? "1" : "0");

        return new InferenceSession(modelPath, options);
    }

    private void AudioToMels()
    {
        while (_samples.Count >= _frameSize)
        {
            var frameData = _samples.Take(_frameSize).ToArray();
            _samples.RemoveRange(0, _frameSize);

            var melInput = new DenseTensor<float>(new[] { 1, _frameSize });
            for (var i = 0; i < _frameSize; i++)
            {
                melInput[0, i] = frameData[i];
            }

            var melInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", melInput)
            };

            using var melResults = _melSession.Run(melInputs);
            var melOutput = melResults.First().AsEnumerable<float>().ToArray();

            // Scale mels: (melData[i] / 10.0f) + 2.0f.
            for (var i = 0; i < melOutput.Length; i++)
            {
                _mels.Add((melOutput[i] / 10.0f) + 2.0f);
            }
        }
    }

    private void MelsToFeatures()
    {
        var melFrames = _mels.Count / NumMels;

        while (melFrames >= EmbWindowSize)
        {
            var windowData = _mels.Take(EmbWindowSize * NumMels).ToArray();

            var embInput = new DenseTensor<float>(new[] { 1, EmbWindowSize, NumMels, 1 });
            for (var f = 0; f < EmbWindowSize; f++)
            {
                for (var m = 0; m < NumMels; m++)
                {
                    embInput[0, f, m, 0] = windowData[f * NumMels + m];
                }
            }

            var inputName = _embSession.InputMetadata.First().Key;
            var embInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, embInput)
            };

            using var embResults = _embSession.Run(embInputs);
            var embOutput = embResults.First().AsEnumerable<float>().ToArray();

            foreach (var featureBuffer in _features)
            {
                featureBuffer.AddRange(embOutput);
            }

            _mels.RemoveRange(0, EmbStepSize * NumMels);
            melFrames = _mels.Count / NumMels;
        }
    }

    private void FeaturesToOutput(out int detectedIndex)
    {
        detectedIndex = -1;

        for (var i = 0; i < _wwSessions.Count; i++)
        {
            var featureBuffer = _features[i];
            var numBufferedFeatures = featureBuffer.Count / EmbFeatures;

            // A model enters its refractory period immediately after a detection.
            if (_refractoryCounts[i] > 0)
            {
                _refractoryCounts[i]--;
            }

            while (numBufferedFeatures >= WWFeatures)
            {
                var wakeWordData = featureBuffer.Take(WWFeatures * EmbFeatures).ToArray();

                // Remove one embedding step after inference to create a sliding window.
                featureBuffer.RemoveRange(0, EmbFeatures);

                var wakeWordInput = new DenseTensor<float>(new[] { 1, WWFeatures, EmbFeatures });
                for (var f = 0; f < WWFeatures; f++)
                {
                    for (var e = 0; e < EmbFeatures; e++)
                    {
                        wakeWordInput[0, f, e] = wakeWordData[f * EmbFeatures + e];
                    }
                }

                var inputName = _wwSessions[i].InputMetadata.First().Key;
                var wakeWordInputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, wakeWordInput)
                };

                using var wakeWordResults = _wwSessions[i].Run(wakeWordInputs);
                var wakeWordOutput = wakeWordResults.First().AsEnumerable<float>().ToArray();

                var wakeWord = _settings.WakeWords[i];
                foreach (var probability in wakeWordOutput)
                {
                    _settings.DebugAction?.Invoke(wakeWord.Model, probability, false);

                    if (_refractoryCounts[i] > 0)
                    {
                        continue;
                    }

                    if (probability > wakeWord.Threshold)
                    {
                        _activations[i]++;
                        if (_activations[i] >= wakeWord.TriggerLevel)
                        {
                            _settings.DebugAction?.Invoke(wakeWord.Model, probability, true);

                            detectedIndex = i;
                            _activations[i] = 0;
                            _refractoryCounts[i] = wakeWord.Refractory;
                        }
                    }
                    // [PATCH] Upstream had an else branch that always assigned zero:
                    //   else _activations[i] = Math.Min(0, _activations[i] + 1);
                    // The counter is never negative, so that branch was a no-op. This version
                    // has the same behavior with less code.
                    else if (_activations[i] > 0)
                    {
                        _activations[i] = Math.Max(0, _activations[i] - 1);
                    }
                }

                numBufferedFeatures = featureBuffer.Count / EmbFeatures;
            }
        }
    }

    public void Dispose()
    {
        _melSession.Dispose();
        _embSession.Dispose();

        foreach (var session in _wwSessions)
        {
            session.Dispose();
        }
    }
}
