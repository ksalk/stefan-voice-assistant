using System.Globalization;

namespace Stefan.Node.WakeWordBenchmark;

internal static class BenchmarkFormatting
{
    public static string Describe(BenchmarkConfiguration configuration) =>
        Describe(configuration.KeywordsFile, configuration.Score, configuration.Threshold);

    public static string Describe(string keywordsFile, float score, float threshold) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(keywordsFile)} · score {score:0.0##} · threshold {threshold:0.0##}");

    public static string OutcomeName(ClipOutcome outcome) => outcome switch
    {
        ClipOutcome.Hit => "hit",
        ClipOutcome.Miss => "miss",
        ClipOutcome.FalseActivation => "false_activation",
        ClipOutcome.Clean => "clean",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    public static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1.0
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMinutes} m {elapsed.Seconds} s")
            : string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0.0} s");

    public static string FormatRecall(BenchmarkSummaryRow row) =>
        row.Recall is null
            ? "-"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{row.DetectedCount}/{row.PositiveCount}  {row.Recall.Value * 100.0:0.0}%");

    public static string FormatLatency(BenchmarkSummaryRow row) =>
        row.AverageLatencyMs is null
            ? "-"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{row.AverageLatencyMs:0} / {row.MedianLatencyMs:0} ms");
}
