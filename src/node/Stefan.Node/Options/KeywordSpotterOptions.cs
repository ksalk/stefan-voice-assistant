namespace Stefan.Node.Options;

public class KeywordSpotterOptions
{
    public const string SectionName = "KeywordSpotter";
    public string ModelPath { get; set; } = "/app/models";
    public string EncoderFile { get; set; } = "encoder-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string DecoderFile { get; set; } = "decoder-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string JoinerFile { get; set; } = "joiner-epoch-13-avg-2-chunk-16-left-64.onnx";
    public string TokensPath { get; set; } = "tokens.txt";
    public string KeywordsFile { get; set; } = "keywords.txt";
    public string StopKeywordsFile { get; set; } = "stop_keywords.txt";
    public int NumThreads { get; set; } = 2;
    public string Provider { get; set; } = "cpu";
    public int FeatureDim { get; set; } = 80;
}
