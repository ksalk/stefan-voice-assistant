using SherpaOnnx;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

public static class KeywordSpotterFactory
{
    public static KeywordSpotter Create(
        KeywordSpotterOptions options,
        string keywordsFile,
        int sampleRate)
    {
        var keywordSpotterConfig = new KeywordSpotterConfig()
        {
            ModelConfig = new OnlineModelConfig()
            {
                Transducer = new OnlineTransducerModelConfig()
                {
                    Encoder = Path.Combine(options.ModelPath, options.EncoderFile),
                    Decoder = Path.Combine(options.ModelPath, options.DecoderFile),
                    Joiner = Path.Combine(options.ModelPath, options.JoinerFile)
                },
                Tokens = Path.Combine(options.ModelPath, options.TokensPath),
                NumThreads = options.NumThreads,
                Provider = options.Provider,
            },
            KeywordsFile = keywordsFile,
            KeywordsScore = options.KeywordsScore,
            KeywordsThreshold = options.KeywordsThreshold,
            FeatConfig = new FeatureConfig()
            {
                SampleRate = sampleRate,
                FeatureDim = options.FeatureDim
            }
        };

        return new KeywordSpotter(keywordSpotterConfig);
    }
}
