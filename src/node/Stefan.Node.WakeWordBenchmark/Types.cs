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

public enum ClipOutcome
{
    Hit,
    Miss,
    FalseActivation,
    Clean,
}

public sealed record BenchmarkResult(
    BenchmarkConfiguration Configuration,
    CorpusClip CorpusClip,
    double AudioDurationMs,
    int DetectionCount,
    double? FirstDetectionOffsetMs,
    double ProcessingTimeMs,
    double PeakWorkingSetMb,
    double Rtf)
{
    public ClipOutcome Outcome
    {
        get
        {
            var isPositive = string.Equals(CorpusClip.Label, "positive", StringComparison.OrdinalIgnoreCase);
            return (isPositive, DetectionCount > 0) switch
            {
                (true, true) => ClipOutcome.Hit,
                (true, false) => ClipOutcome.Miss,
                (false, true) => ClipOutcome.FalseActivation,
                (false, false) => ClipOutcome.Clean,
            };
        }
    }

    public double? LatencyMs =>
        FirstDetectionOffsetMs is not null && CorpusClip.WakeWordEndMs is not null
            ? FirstDetectionOffsetMs.Value - CorpusClip.WakeWordEndMs.Value
            : null;
}

public sealed record BenchmarkSummary(IReadOnlyList<BenchmarkSummaryRow> Rows)
{
    public IReadOnlyList<BenchmarkSummaryRow> Rank() => Rows
        .OrderByDescending(row => row.Recall ?? -1.0)
        .ThenBy(row => row.FalseActivationsPerHour ?? double.MaxValue)
        .ThenBy(row => row.Rtf)
        .ToArray();
}

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
