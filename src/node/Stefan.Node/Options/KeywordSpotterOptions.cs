namespace Stefan.Node.Options;

/// <summary>One keyword pronunciation handled by its own KeywordSpotter instance.</summary>
public class KeywordGroupOptions
{
    public string KeywordsFile { get; set; } = "keywords.txt";
    public float KeywordsScore { get; set; } = 2.0f;
    public float KeywordsThreshold { get; set; } = 0.1f;
}

public class KeywordSpotterOptions
{
    public const string SectionName = "KeywordSpotter";
    public string ModelPath { get; set; } = "/app/models";
    public string EncoderFile { get; set; } = "encoder-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string DecoderFile { get; set; } = "decoder-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string JoinerFile { get; set; } = "joiner-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string TokensPath { get; set; } = "tokens.txt";
    public string KeywordsFile { get; set; } = "keywords.txt";
    public string StopKeywordsFile { get; set; } = "WakeWordDetection/sherpa-onnx/stop-keywords.txt";
    public int NumThreads { get; set; } = 2;
    public string Provider { get; set; } = "cpu";
    public int FeatureDim { get; set; } = 80;
    public float KeywordsScore { get; set; } = 1.0f; // default SherpaOnnx value
    public float KeywordsThreshold { get; set; } = 0.25f; // default SherpaOnnx value
    public int MaxActivePaths { get; set; } = 4; // default SherpaOnnx value
    public int NumTrailingBlanks { get; set; } = 1; // default SherpaOnnx value

    /// <summary>
    /// Optional per-pronunciation keyword groups. When set, each group runs in its own KeywordSpotter
    /// instance so accent variants cannot interfere with each other inside one context graph.
    /// The shared KeywordsScore / KeywordsThreshold and KeywordsFile are ignored in this mode.
    /// </summary>
    public KeywordGroupOptions[] Groups { get; set; } = [];
}
