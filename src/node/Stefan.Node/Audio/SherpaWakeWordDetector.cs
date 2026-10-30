using Microsoft.Extensions.Options;
using SherpaOnnx;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

public sealed class SherpaWakeWordDetector : IWakeWordDetector
{
    private readonly KeywordSpotter _keywordSpotter;
    private OnlineStream _keywordStream;

    public SherpaWakeWordDetector(
        IOptions<KeywordSpotterOptions> keywordSpotterOptions,
        IOptions<AudioOptions> audioOptions)
    {
        var options = keywordSpotterOptions.Value;
        _keywordSpotter = KeywordSpotterFactory.Create(
            options,
            options.KeywordsFile,
            audioOptions.Value.Input.ProcessingSampleRate);
        _keywordStream = _keywordSpotter.CreateStream();
    }

    public string? Process(RawPcmChunk chunk)
    {
        var monoSamples = AudioProcessing.ConvertPcm16ToFloat(chunk);
        if (monoSamples.Length == 0)
        {
            return null;
        }

        _keywordStream.AcceptWaveform(chunk.Format.SampleRate, monoSamples);

        while (_keywordSpotter.IsReady(_keywordStream))
        {
            _keywordSpotter.Decode(_keywordStream);

            var keywordResult = _keywordSpotter.GetResult(_keywordStream);
            if (!string.IsNullOrWhiteSpace(keywordResult.Keyword))
            {
                Reset();
                return keywordResult.Keyword;
            }
        }

        return null;
    }

    public void Reset()
    {
        // Sherpa's KeywordSpotter.Reset only clears the decoder hypothesis; the
        // stream keeps the accumulated audio context and encoder state. That
        // makes the next utterance depend on whatever preceded the reset. A
        // fresh stream gives every utterance a pristine decoding state.
        _keywordStream.Dispose();
        _keywordStream = _keywordSpotter.CreateStream();
    }

    public void Dispose()
    {
        _keywordStream.Dispose();
        _keywordSpotter.Dispose();
    }
}
