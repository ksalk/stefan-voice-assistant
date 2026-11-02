using Microsoft.Extensions.Options;
using SherpaOnnx;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

/// <summary>
/// Keyword spotting with sherpa-onnx. Supports one shared KeywordSpotter (single keywords file)
/// or several independent KeywordSpotters, one per pronunciation group. Independent spotters keep
/// the variants from sharing one search beam, so a boosted accent variant cannot push another
/// variant's tokens around.
/// </summary>
public sealed class SherpaWakeWordDetector : IWakeWordDetector
{
    private sealed class Spotter : IDisposable
    {
        public required string Tag { get; init; }
        public required KeywordSpotter KeywordSpotter { get; init; }

        public OnlineStream Stream { get; set; } = null!;

        public void Dispose()
        {
            Stream.Dispose();
            KeywordSpotter.Dispose();
        }
    }

    private readonly Spotter[] _spotters;

    public SherpaWakeWordDetector(
        IOptions<KeywordSpotterOptions> keywordSpotterOptions,
        IOptions<AudioOptions> audioOptions)
    {
        var options = keywordSpotterOptions.Value;
        var sampleRate = audioOptions.Value.Input.ProcessingSampleRate;

        if (options.Groups.Length > 0)
        {
            var groups = options.Groups;
            _spotters = new Spotter[groups.Length];
            for (var i = 0; i < groups.Length; i++)
            {
                // In group mode the group's own score and threshold win. Clone the shared options
                // and override so KeywordSpotterFactory keeps its single-code path.
                var groupOptions = Clone(options);
                groupOptions.KeywordsFile = groups[i].KeywordsFile;
                groupOptions.KeywordsScore = groups[i].KeywordsScore;
                groupOptions.KeywordsThreshold = groups[i].KeywordsThreshold;
                _spotters[i] = CreateSpotter($"{i}:{Path.GetFileName(groupOptions.KeywordsFile)}", groupOptions, sampleRate);
            }
        }
        else
        {
            _spotters = [CreateSpotter(Path.GetFileName(options.KeywordsFile), options, sampleRate)];
        }
    }

    private static KeywordSpotterOptions Clone(KeywordSpotterOptions source)
    {
        var clone = new KeywordSpotterOptions
        {
            ModelPath = source.ModelPath,
            EncoderFile = source.EncoderFile,
            DecoderFile = source.DecoderFile,
            JoinerFile = source.JoinerFile,
            TokensPath = source.TokensPath,
            KeywordsFile = source.KeywordsFile,
            StopKeywordsFile = source.StopKeywordsFile,
            NumThreads = source.NumThreads,
            Provider = source.Provider,
            FeatureDim = source.FeatureDim,
            KeywordsScore = source.KeywordsScore,
            KeywordsThreshold = source.KeywordsThreshold,
            MaxActivePaths = source.MaxActivePaths,
            NumTrailingBlanks = source.NumTrailingBlanks,
            Groups = source.Groups,
        };
        return clone;
    }

    private static Spotter CreateSpotter(string tag, KeywordSpotterOptions options, int sampleRate)
    {
        var keywordSpotter = KeywordSpotterFactory.Create(options, options.KeywordsFile, sampleRate);
        return new Spotter
        {
            Tag = tag,
            KeywordSpotter = keywordSpotter,
            Stream = keywordSpotter.CreateStream(),
        };
    }

    public string? Process(RawPcmChunk chunk)
    {
        var monoSamples = AudioProcessing.ConvertPcm16ToFloat(chunk);
        if (monoSamples.Length == 0)
        {
            return null;
        }

        string? detected = null;
        foreach (var spotter in _spotters)
        {
            var keyword = ProcessSpotter(spotter, chunk.Format.SampleRate, monoSamples);
            if (detected is null && keyword is not null)
            {
                detected = keyword;
            }
        }

        if (detected is not null)
        {
            // Wake handling pauses the detector until the command session closes. Keeping all
            // members in a fresh state means every member resumes with clean audio context.
            foreach (var spotter in _spotters)
            {
                ResetSpotter(spotter);
            }
        }

        return detected;
    }

    private string? ProcessSpotter(Spotter spotter, int sampleRate, float[] samples)
    {
        spotter.Stream.AcceptWaveform(sampleRate, samples);
        while (spotter.KeywordSpotter.IsReady(spotter.Stream))
        {
            spotter.KeywordSpotter.Decode(spotter.Stream);

            var keywordResult = spotter.KeywordSpotter.GetResult(spotter.Stream);
            if (string.IsNullOrWhiteSpace(keywordResult.Keyword))
            {
                continue;
            }

            ResetSpotter(spotter);
            return keywordResult.Keyword;
        }

        return null;
    }

    public void Reset()
    {
        foreach (var spotter in _spotters)
        {
            ResetSpotter(spotter);
        }
    }

    private void ResetSpotter(Spotter spotter)
    {
        // Sherpa's KeywordSpotter.Reset only clears the decoder hypothesis; the
        // stream keeps the accumulated audio context and encoder state. That
        // makes the next utterance depend on whatever preceded the reset. A
        // fresh stream gives every utterance a pristine decoding state.
        spotter.Stream.Dispose();
        spotter.Stream = spotter.KeywordSpotter.CreateStream();
    }

    public void Dispose()
    {
        foreach (var spotter in _spotters)
        {
            spotter.Dispose();
        }
    }
}
