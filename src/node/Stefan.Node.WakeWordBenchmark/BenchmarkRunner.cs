using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.Options;

namespace Stefan.Node.WakeWordBenchmark;

public sealed class BenchmarkRunner
{
    private readonly BenchmarkOptions _options;

    public BenchmarkRunner(BenchmarkOptions options)
    {
        _options = options;
    }

    public BenchmarkSummary Run()
    {
        var configurations = BenchmarkConfigurations.Create(_options);
        var manifest = CorpusManifestLoader.Load(_options.ManifestPath);
        var results = new List<BenchmarkResult>();
        var stopwatch = Stopwatch.StartNew();
        var process = Process.GetCurrentProcess();
        var reporter = new ConsoleReporter();

        for (var configurationIndex = 0; configurationIndex < configurations.Count; configurationIndex++)
        {
            var configuration = configurations[configurationIndex];
            reporter.BeginConfiguration(configurationIndex + 1, configurations.Count, configuration);
            var configurationStopwatch = Stopwatch.StartNew();
            var clipsDone = 0;
            var positivesDone = 0;
            var hits = 0;
            var falseAlarms = 0;

            using var detector = CreateDetector(configuration);

            foreach (var clip in manifest.Clips)
            {
                var wav = PcmWavReader.Read(clip.FileName);
                var chunks = ReplayChunks(wav, _options.ChunkBytes, _options.TailMs, _options.LeadMs);
                var clipDurationMs = ClipDurationMs(wav);
                detector.Reset();

                var processingStopwatch = Stopwatch.StartNew();
                var audioDurationMs = 0.0;
                var firstDetectionOffsetMs = (double?)null;
                var detectionCount = 0;

                foreach (var chunk in chunks)
                {
                    var processedChunk = _options.NoResample
                        ? chunk
                        : AudioProcessing.ConvertAndResample(chunk, _options.ProcessingSampleRate);
                    var keyword = detector.Process(processedChunk);
                    audioDurationMs += AudioDurationMs(processedChunk);
                    if (keyword is not null)
                    {
                        detectionCount++;
                        // Report detection offsets relative to the clip start, not the lead-in silence.
                        firstDetectionOffsetMs ??= Math.Max(0.0, audioDurationMs - _options.LeadMs);
                    }
                }
                processingStopwatch.Stop();

                var rtf = audioDurationMs > 0.0
                    ? processingStopwatch.Elapsed.TotalMilliseconds / audioDurationMs
                    : (double?)null;
                var peakWorkingSetMb = process.PeakWorkingSet64 / 1024.0 / 1024.0;

                var result = new BenchmarkResult(
                    configuration,
                    clip,
                    audioDurationMs,
                    clipDurationMs,
                    detectionCount,
                    firstDetectionOffsetMs,
                    processingStopwatch.Elapsed.TotalMilliseconds,
                    peakWorkingSetMb,
                    rtf ?? 0.0);
                results.Add(result);

                clipsDone++;
                if (result.Outcome is ClipOutcome.Hit or ClipOutcome.Miss)
                {
                    positivesDone++;
                }

                if (result.Outcome == ClipOutcome.Hit)
                {
                    hits++;
                }
                else if (result.Outcome == ClipOutcome.FalseActivation)
                {
                    falseAlarms++;
                }

                reporter.UpdateProgress(
                    clipsDone,
                    manifest.Clips.Count,
                    positivesDone,
                    hits,
                    falseAlarms,
                    configurationStopwatch.Elapsed);
            }

            reporter.EndConfiguration(positivesDone, hits, falseAlarms, configurationStopwatch.Elapsed);
        }

        stopwatch.Stop();
        var summary = Summarize(configurations, results, process, stopwatch.ElapsedMilliseconds);
        var runDirectory = BenchmarkReportWriter.Write(_options, manifest, results, summary);
        reporter.PrintReport(_options, manifest, results, summary, runDirectory, stopwatch.Elapsed);
        return summary;
    }

    private IWakeWordDetector CreateDetector(BenchmarkConfiguration configuration)
    {
        return _options.Detector switch
        {
            "sherpa" => new SherpaWakeWordDetector(
                Microsoft.Extensions.Options.Options.Create(configuration.KeywordSpotterOptions),
                Microsoft.Extensions.Options.Options.Create(configuration.AudioOptions)),
            "nanowakeword" => new NanoWakeWordDetector(
                Microsoft.Extensions.Options.Options.Create(
                    configuration.NanoWakeWordOptions
                    ?? throw new InvalidOperationException("NanoWakeWord configuration is missing.")),
                NullLogger<NanoWakeWordDetector>.Instance),
            _ => throw new InvalidOperationException($"Unknown detector '{_options.Detector}'."),
        };
    }

    private static double AudioDurationMs(RawPcmChunk chunk)
    {
        var bytesPerSample = chunk.Format.BitsPerSample / 8;
        return chunk.Bytes.Length * 1000.0 / bytesPerSample / chunk.Format.SampleRate;
    }

    private static double ClipDurationMs(PcmWav wav)
    {
        var bytesPerSample = wav.Format.BitsPerSample / 8;
        return wav.Data.Length * 1000.0 / bytesPerSample / wav.Format.SampleRate;
    }

    public static IEnumerable<RawPcmChunk> ReplayChunks(
        PcmWav wav,
        int requestedChunkBytes,
        int tailMs,
        int leadMs = 0)
    {
        var frameBytes = wav.Format.Channels * (wav.Format.BitsPerSample / 8);
        var chunkBytes = Math.Max(frameBytes, (requestedChunkBytes / frameBytes) * frameBytes);
        var leadBytes = (int)Math.Ceiling(wav.Format.SampleRate * frameBytes * leadMs / 1000.0);
        leadBytes = (leadBytes + frameBytes - 1) / frameBytes * frameBytes;
        var leadData = leadMs > 0 ? new byte[leadBytes] : Array.Empty<byte>();
        var tailBytes = (int)Math.Ceiling(wav.Format.SampleRate * frameBytes * tailMs / 1000.0);
        tailBytes = (tailBytes + frameBytes - 1) / frameBytes * frameBytes;
        var tailData = tailMs > 0 ? new byte[tailBytes] : Array.Empty<byte>();
        var data = wav.Data;

        foreach (var chunk in ReplaySilence(leadData, chunkBytes, wav.Format))
        {
            yield return chunk;
        }

        for (var offset = 0; offset < data.Length; offset += chunkBytes)
        {
            var bytes = Math.Min(data.Length - offset, chunkBytes);
            var buffer = new byte[bytes];
            data.AsSpan(offset, bytes).CopyTo(buffer);
            yield return new RawPcmChunk { Bytes = buffer, Format = wav.Format };
        }

        foreach (var chunk in ReplaySilence(tailData, chunkBytes, wav.Format))
        {
            yield return chunk;
        }
    }

    private static IEnumerable<RawPcmChunk> ReplaySilence(byte[] silence, int chunkBytes, AudioFormat format)
    {
        for (var offset = 0; offset < silence.Length; offset += chunkBytes)
        {
            var bytes = Math.Min(silence.Length - offset, chunkBytes);
            var buffer = new byte[bytes];
            silence.AsSpan(offset, bytes).CopyTo(buffer);
            yield return new RawPcmChunk { Bytes = buffer, Format = format };
        }
    }

    private static BenchmarkSummary Summarize(
        IReadOnlyList<BenchmarkConfiguration> configurations,
        IReadOnlyList<BenchmarkResult> results,
        Process process,
        long elapsedMs)
    {
        var rows = new List<BenchmarkSummaryRow>();
        foreach (var configuration in configurations)
        {
            var configurationResults = results
                .Where(result => result.Configuration.Id == configuration.Id)
                .ToArray();
            var positives = configurationResults
                .Where(result => string.Equals(result.CorpusClip.Label, "positive", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var negatives = configurationResults
                .Where(result => string.Equals(result.CorpusClip.Label, "negative", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var detectedCount = positives.Count(result => result.Outcome == ClipOutcome.Hit);
            var falseActivations = negatives.Sum(result => result.DetectionCount);
            // Use the real clip duration, not the padded replay. Lead-in and tail silence would
            // otherwise dilute the false-activation rate.
            var negativeHours = negatives.Sum(result => result.ClipDurationMs) / 3_600_000.0;
            var falseActivationsPerHour = negativeHours > 0.0
                ? falseActivations / negativeHours
                : (double?)null;
            var latencyValues = positives
                .Select(result => result.LatencyMs)
                .Where(latency => latency is not null)
                .Select(latency => latency!.Value)
                .OrderBy(item => item)
                .ToArray();
            var averageLatencyMs = latencyValues.Length > 0 ? latencyValues.Average() : (double?)null;
            var medianLatencyMs = latencyValues.Length > 0
                ? (latencyValues.Length % 2 == 1
                    ? latencyValues[latencyValues.Length / 2]
                    : (latencyValues[latencyValues.Length / 2 - 1] + latencyValues[latencyValues.Length / 2]) / 2.0)
                : (double?)null;
            var peakWorkingSetMb = process.PeakWorkingSet64 / 1024.0 / 1024.0;
            var rtf = positives.Sum(result => result.AudioDurationMs) > 0.0
                ? positives.Sum(result => result.ProcessingTimeMs) / positives.Sum(result => result.AudioDurationMs)
                : (double?)null;

            rows.Add(new BenchmarkSummaryRow(
                configuration.Id,
                configuration.KeywordsFile,
                configuration.Score,
                configuration.Threshold,
                positives.Length,
                detectedCount,
                positives.Length > 0 ? (double)detectedCount / positives.Length : null,
                falseActivations,
                negativeHours,
                falseActivationsPerHour,
                averageLatencyMs,
                medianLatencyMs,
                peakWorkingSetMb,
                rtf ?? 0.0));
        }

        return new BenchmarkSummary(rows);
    }

}

internal static class BenchmarkConfigurations
{
    public static List<BenchmarkConfiguration> Create(BenchmarkOptions options)
    {
        if (string.Equals(options.Detector, "nanowakeword", StringComparison.OrdinalIgnoreCase))
        {
            return CreateNanoWakeWordConfigurations(options);
        }

        if (options.EnsembleMembers.Count > 0)
        {
            // One configuration: the ensemble itself. Score/threshold sweeps do not apply;
            // each member carries its own per-line score and threshold.
            var configuration = new BenchmarkConfiguration(
                $"ens|{string.Join("+", options.EnsembleMembers.Select(m => $"{Path.GetFileName(m.KeywordsFile)}:{m.Score:0.####}/{m.Threshold:0.####}"))}",
                string.Join(", ", options.EnsembleMembers.Select(m => Path.GetFileName(m.KeywordsFile))),
                0,
                0,
                CreateKeywordSpotterOptions(options),
                CreateAudioOptions(options));
            return [configuration];
        }

        var configurations = new List<BenchmarkConfiguration>();
        var index = 0;
        foreach (var score in options.Scores)
        {
            foreach (var threshold in options.Thresholds)
            {
                foreach (var keywordsFile in options.KeywordFiles)
                {
                    foreach (var maxActivePaths in options.MaxActivePaths)
                    {
                        foreach (var numTrailingBlanks in options.NumTrailingBlanks)
                        {
                            configurations.Add(new BenchmarkConfiguration(
                                $"{index:D3}|{Path.GetFileName(keywordsFile)}|score:{score:0.####}|threshold:{threshold:0.####}|paths:{maxActivePaths}|blanks:{numTrailingBlanks}",
                                keywordsFile,
                                score,
                                threshold,
                                CreateKeywordSpotterOptions(options, keywordsFile, score, threshold, maxActivePaths, numTrailingBlanks),
                                CreateAudioOptions(options)));
                            index++;
                        }
                    }
                }
            }
        }

        return configurations;
    }

    private static List<BenchmarkConfiguration> CreateNanoWakeWordConfigurations(BenchmarkOptions options)
    {
        var configurations = new List<BenchmarkConfiguration>();
        var index = 0;
        foreach (var threshold in options.NanoThresholds)
        {
            var nano = new NanoWakeWordOptions
            {
                Model = options.NanoWakeWordOptions.Model,
                Keyword = options.NanoWakeWordOptions.Keyword,
                Threshold = threshold,
                TriggerLevel = options.NanoWakeWordOptions.TriggerLevel,
                Refractory = options.NanoWakeWordOptions.Refractory,
                StepFrames = options.NanoWakeWordOptions.StepFrames,
            };

            configurations.Add(new BenchmarkConfiguration(
                $"{index:D3}|nano|{nano.Model}|threshold:{threshold:0.####}|trigger:{nano.TriggerLevel}|refractory:{nano.Refractory}|step:{nano.StepFrames}",
                $"{nano.Model}.onnx",
                0,
                threshold,
                CreateKeywordSpotterOptions(options),
                CreateAudioOptions(options),
                nano));
            index++;
        }

        return configurations;
    }

    private static KeywordSpotterOptions CreateKeywordSpotterOptions(BenchmarkOptions options)
    {
        var settings = options.KeywordSpotterOptions;
        return new KeywordSpotterOptions
        {
            ModelPath = settings.ModelPath,
            EncoderFile = options.EncoderFile,
            DecoderFile = options.DecoderFile,
            JoinerFile = options.JoinerFile,
            TokensPath = settings.TokensPath,
            KeywordsFile = settings.KeywordsFile,
            StopKeywordsFile = settings.StopKeywordsFile,
            NumThreads = settings.NumThreads,
            Provider = settings.Provider,
            FeatureDim = settings.FeatureDim,
            KeywordsScore = settings.KeywordsScore,
            KeywordsThreshold = settings.KeywordsThreshold,
            MaxActivePaths = settings.MaxActivePaths,
            NumTrailingBlanks = settings.NumTrailingBlanks,
            Groups = settings.Groups,
        };
    }

    private static KeywordSpotterOptions CreateKeywordSpotterOptions(
        BenchmarkOptions options,
        string keywordsFile,
        float score,
        float threshold,
        int maxActivePaths,
        int numTrailingBlanks)
    {
        var settings = options.KeywordSpotterOptions;
        return new KeywordSpotterOptions
        {
            ModelPath = settings.ModelPath,
            EncoderFile = options.EncoderFile,
            DecoderFile = options.DecoderFile,
            JoinerFile = options.JoinerFile,
            TokensPath = settings.TokensPath,
            KeywordsFile = keywordsFile,
            StopKeywordsFile = settings.StopKeywordsFile,
            NumThreads = settings.NumThreads,
            Provider = settings.Provider,
            FeatureDim = settings.FeatureDim,
            KeywordsScore = score,
            KeywordsThreshold = threshold,
            MaxActivePaths = maxActivePaths,
            NumTrailingBlanks = numTrailingBlanks,
        };
    }

    private static AudioOptions CreateAudioOptions(BenchmarkOptions options)
    {
        return new AudioOptions
        {
            Input = new AudioInputOptions
            {
                SampleRate = options.ProcessingSampleRate,
                Channels = 1,
                BitsPerSample = 16,
                ProcessingSampleRate = options.ProcessingSampleRate,
            },
            Output = new AudioOutputOptions
            {
                DeviceName = "null",
                VolumeControlName = "Master",
            },
        };
    }
}
