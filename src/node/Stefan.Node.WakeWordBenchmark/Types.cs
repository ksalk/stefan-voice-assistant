using Stefan.Node.Options;

namespace Stefan.Node.WakeWordBenchmark;

public sealed record CorpusManifest(IReadOnlyList<CorpusClip> Clips);

public sealed record CorpusClip(
    string FileName,
    string RelativePath,
    string Label,
    double? WakeWordEndMs,
    IReadOnlyList<string> Tags);

public sealed record BenchmarkConfiguration(
    string Id,
    string KeywordsFile,
    float Score,
    float Threshold,
    KeywordSpotterOptions KeywordSpotterOptions,
    AudioOptions AudioOptions);

public sealed record BenchmarkResult(
    BenchmarkConfiguration Configuration,
    CorpusClip CorpusClip,
    double AudioDurationMs,
    int DetectionCount,
    double? FirstDetectionOffsetMs,
    double ProcessingTimeMs,
    double PeakWorkingSetMb,
    double Rtf);

public sealed record BenchmarkSummary(IReadOnlyList<BenchmarkSummaryRow> Rows);

public sealed record BenchmarkSummaryRow(
    string ConfigurationId,
    string KeywordsFile,
    float Score,
    float Threshold,
    int PositiveCount,
    int DetectedCount,
    double? Recall,
    int FalseActivations,
    double NegativeHours,
    double? FalseActivationsPerHour,
    double? AverageLatencyMs,
    double? MedianLatencyMs,
    double PeakWorkingSetMb,
    double Rtf)
{
    public int MissedCount => Math.Max(0, PositiveCount - DetectedCount);
}
