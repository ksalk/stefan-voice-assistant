using System.Globalization;

namespace Stefan.Node.WakeWordBenchmark;

public sealed class ConsoleReporter
{
    private const int BarWidth = 30;
    private const int MaxListedClips = 10;
    private readonly bool _interactive;
    private int _lastProgressLength;

    public ConsoleReporter()
    {
        _interactive = !Console.IsOutputRedirected;
    }

    public void BeginConfiguration(int index, int total, BenchmarkConfiguration configuration)
    {
        Console.WriteLine($"Configuration {index}/{total} · {BenchmarkFormatting.Describe(configuration)}");
    }

    public void UpdateProgress(
        int clipsDone,
        int clipsTotal,
        int positivesDone,
        int hits,
        int falseAlarms,
        TimeSpan elapsed)
    {
        if (!_interactive)
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"  [{BuildBar(clipsDone, clipsTotal)}] {clipsDone}/{clipsTotal} clips" +
            $" · hits {hits}/{positivesDone} · FA {falseAlarms} · {BenchmarkFormatting.FormatElapsed(elapsed)}");
        Console.Write('\r' + line.PadRight(Math.Max(_lastProgressLength, line.Length)));
        _lastProgressLength = line.Length;
    }

    public void EndConfiguration(int positives, int hits, int falseAlarms, TimeSpan elapsed)
    {
        if (_interactive)
        {
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine(
                $"  done in {BenchmarkFormatting.FormatElapsed(elapsed)} · hits {hits}/{positives} · FA {falseAlarms}");
        }

        _lastProgressLength = 0;
    }

    public void PrintReport(
        BenchmarkOptions options,
        CorpusManifest manifest,
        IReadOnlyList<BenchmarkResult> results,
        BenchmarkSummary summary,
        string runDirectory,
        TimeSpan elapsed)
    {
        var ranked = summary.Rank();
        var positiveCount = manifest.Clips.Count(
            clip => string.Equals(clip.Label, "positive", StringComparison.OrdinalIgnoreCase));
        var negativeCount = manifest.Clips.Count - positiveCount;
        var negativeHours = ranked.FirstOrDefault()?.NegativeHours ?? 0.0;
        var configurationWord = ranked.Count == 1 ? "configuration" : "configurations";

        Console.WriteLine();
        Console.WriteLine(new string('=', 75));
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"RESULTS · {manifest.Clips.Count} clips ({positiveCount} positive / {negativeCount} negative," +
                $" {negativeHours:0.###} h) · {ranked.Count} {configurationWord}"));
        Console.WriteLine(new string('=', 75));
        Console.WriteLine(
            $"Model {options.ModelPath} · chunk {options.ChunkBytes} B · tail {options.TailMs} ms" +
            $" · {options.ProcessingSampleRate} Hz");
        Console.WriteLine();

        if (ranked.Count > 0)
        {
            PrintLeaderboard(ranked);
            PrintLegend();
            if (ranked.All(row => row.AverageLatencyMs is null))
            {
                Console.WriteLine();
                Console.WriteLine("Latency unavailable: manifest clips have no wakeWordEndMs.");
            }

            PrintBestConfigFailures(ranked[0], results);
        }

        Console.WriteLine();
        Console.WriteLine($"Completed in {BenchmarkFormatting.FormatElapsed(elapsed)}");
        Console.WriteLine($"Report: {Path.Combine(runDirectory, "report.md")}");
    }

    private static void PrintLeaderboard(IReadOnlyList<BenchmarkSummaryRow> ranked)
    {
        var headers = new[] { "Rank", "Config", "Score", "Thr", "Recall", "FA/h", "Latency avg/p50", "RTF", "Peak mem" };
        var rows = ranked.Select((row, index) => new[]
        {
            index == 0 ? $"* {index + 1}" : $"  {index + 1}",
            Path.GetFileName(row.KeywordsFile),
            row.Score.ToString("0.0##", CultureInfo.InvariantCulture),
            row.Threshold.ToString("0.0##", CultureInfo.InvariantCulture),
            BenchmarkFormatting.FormatRecall(row),
            row.FalseActivationsPerHour?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-",
            BenchmarkFormatting.FormatLatency(row),
            row.Rtf.ToString("0.000", CultureInfo.InvariantCulture),
            row.PeakWorkingSetMb.ToString("0", CultureInfo.InvariantCulture) + " MB",
        }).ToArray();

        var widths = new int[headers.Length];
        for (var column = 0; column < headers.Length; column++)
        {
            widths[column] = Math.Max(
                headers[column].Length,
                rows.Max(row => row[column].Length));
        }

        PrintTableRow(headers, widths);
        Console.WriteLine(string.Join("  ", widths.Select(width => new string('─', width))));
        foreach (var row in rows)
        {
            PrintTableRow(row, widths);
        }
    }

    private static void PrintTableRow(IReadOnlyList<string> cells, IReadOnlyList<int> widths)
    {
        var padded = cells.Select((cell, column) => cell.PadRight(widths[column]));
        Console.WriteLine(string.Join("  ", padded));
    }

    private static void PrintLegend()
    {
        Console.WriteLine();
        Console.WriteLine("Legend:");
        Console.WriteLine("  Rank      position after sorting: recall first, then false activations. * marks the best.");
        Console.WriteLine("  Config    keywords file used for this configuration");
        Console.WriteLine("  Score     keywords_score — boost added to tokens of the keyword. Higher means easier detection.");
        Console.WriteLine("  Thr       keywords_threshold — minimum token score to emit a detection. Higher means stricter.");
        Console.WriteLine("  Recall    detected positives / all positive clips in the corpus");
        Console.WriteLine("  FA/h      false activations per hour of negative audio");
        Console.WriteLine("  Latency   detection offset after the wake word ends, avg / p50. Needs wakeWordEndMs in the manifest.");
        Console.WriteLine("  RTF       real-time factor: processing time / audio time. Lower is faster.");
        Console.WriteLine("  Peak mem  peak process working set while running.");
    }

    private static void PrintBestConfigFailures(
        BenchmarkSummaryRow best,
        IReadOnlyList<BenchmarkResult> results)
    {
        var bestResults = results
            .Where(result => result.Configuration.Id == best.ConfigurationId)
            .ToArray();
        var missed = bestResults.Where(result => result.Outcome == ClipOutcome.Miss).ToArray();
        var falseActivations = bestResults
            .Where(result => result.Outcome == ClipOutcome.FalseActivation)
            .ToArray();
        var description = BenchmarkFormatting.Describe(best.KeywordsFile, best.Score, best.Threshold);

        Console.WriteLine();
        Console.WriteLine($"Missed wake words (best: {description}):");
        PrintClipList(missed, _ => "no detection");
        Console.WriteLine("False activations (best config):");
        PrintClipList(
            falseActivations,
            result => string.Create(
                CultureInfo.InvariantCulture,
                $"fired @ {result.FirstDetectionOffsetMs:0} ms"));
    }

    private static void PrintClipList(
        IReadOnlyList<BenchmarkResult> clips,
        Func<BenchmarkResult, string> describe)
    {
        if (clips.Count == 0)
        {
            Console.WriteLine("  none");
            return;
        }

        var width = clips.Max(clip => clip.CorpusClip.RelativePath.Length);
        foreach (var clip in clips.Take(MaxListedClips))
        {
            Console.WriteLine($"  {clip.CorpusClip.RelativePath.PadRight(width)}  {describe(clip)}");
        }

        if (clips.Count > MaxListedClips)
        {
            Console.WriteLine($"  … and {clips.Count - MaxListedClips} more");
        }
    }

    private static string BuildBar(int done, int total)
    {
        var filled = total <= 0
            ? BarWidth
            : Math.Clamp((int)Math.Round(BarWidth * done / (double)total), 0, BarWidth);
        return filled >= BarWidth
            ? new string('=', BarWidth)
            : new string('=', filled) + '>' + new string('.', BarWidth - filled - 1);
    }
}
