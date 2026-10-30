using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stefan.Node.WakeWordBenchmark;

public static class BenchmarkReportWriter
{
    private const int MaxListedClipsPerSection = 20;

    public static string Write(
        BenchmarkOptions options,
        CorpusManifest manifest,
        IReadOnlyList<BenchmarkResult> results,
        BenchmarkSummary summary)
    {
        var timestamp = DateTime.Now;
        var runDirectory = Path.Combine(
            options.OutputDirectory,
            timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "summary.csv"), FormatSummary(summary));
        File.WriteAllText(Path.Combine(runDirectory, "results.csv"), FormatResults(results));
        File.WriteAllText(Path.Combine(runDirectory, "results.jsonl"), FormatJsonl(results));
        File.WriteAllText(
            Path.Combine(runDirectory, "report.md"),
            FormatReport(options, manifest, results, summary, timestamp));
        File.WriteAllText(Path.Combine(runDirectory, "configuration.json"), JsonSerializer.Serialize(new
        {
            manifest_path = options.ManifestPath,
            output_directory = runDirectory,
            model_path = options.ModelPath,
            keywords = options.KeywordFiles,
            keyword_transcriptions = options.KeywordTranscriptions,
            processing_sample_rate = options.ProcessingSampleRate,
            chunk_bytes = options.ChunkBytes,
            tail_ms = options.TailMs,
            threads = options.Threads,
            audio_options = options.AudioOptions,
            keyword_spotter_options = options.KeywordSpotterOptions,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
        }));
        return runDirectory;
    }

    private static string FormatSummary(BenchmarkSummary summary)
    {
        var builder = new StringBuilder("configuration_id,keywords_file,score,threshold,positive_count,detected_count,missed_count,false_activations,negative_hours,false_activations_per_hour,average_latency_ms,median_latency_ms,peak_working_set_mb,rtf\n");
        foreach (var row in summary.Rows)
        {
            builder.Append(Csv(row.ConfigurationId));
            builder.Append(',');
            builder.Append(Csv(row.KeywordsFile));
            builder.Append(',');
            builder.Append(FormatNumber(row.Score));
            builder.Append(',');
            builder.Append(FormatNumber(row.Threshold));
            builder.Append(',');
            builder.Append(FormatNumber(row.PositiveCount));
            builder.Append(',');
            builder.Append(FormatNumber(row.DetectedCount));
            builder.Append(',');
            builder.Append(FormatNumber(row.MissedCount));
            builder.Append(',');
            builder.Append(FormatNumber(row.FalseActivations));
            builder.Append(',');
            builder.Append(FormatNumber(row.NegativeHours));
            builder.Append(',');
            builder.Append(FormatNumber(row.FalseActivationsPerHour));
            builder.Append(',');
            builder.Append(FormatNumber(row.AverageLatencyMs));
            builder.Append(',');
            builder.Append(FormatNumber(row.MedianLatencyMs));
            builder.Append(',');
            builder.Append(FormatNumber(row.PeakWorkingSetMb));
            builder.Append(',');
            builder.Append(FormatNumber(row.Rtf));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string FormatResults(IReadOnlyList<BenchmarkResult> results)
    {
        var builder = new StringBuilder("configuration_id,keywords_file,score,threshold,clip_path,label,outcome,audio_duration_ms,detection_count,first_detection_offset_ms,latency_ms,processing_time_ms,peak_working_set_mb,rtf\n");
        foreach (var result in results)
        {
            builder.Append(Csv(result.Configuration.Id));
            builder.Append(',');
            builder.Append(Csv(result.Configuration.KeywordsFile));
            builder.Append(',');
            builder.Append(FormatNumber(result.Configuration.Score));
            builder.Append(',');
            builder.Append(FormatNumber(result.Configuration.Threshold));
            builder.Append(',');
            builder.Append(Csv(result.CorpusClip.RelativePath));
            builder.Append(',');
            builder.Append(result.CorpusClip.Label);
            builder.Append(',');
            builder.Append(BenchmarkFormatting.OutcomeName(result.Outcome));
            builder.Append(',');
            builder.Append(FormatNumber(result.AudioDurationMs));
            builder.Append(',');
            builder.Append(FormatNumber(result.DetectionCount));
            builder.Append(',');
            builder.Append(FormatNumber(result.FirstDetectionOffsetMs));
            builder.Append(',');
            builder.Append(FormatNumber(result.LatencyMs));
            builder.Append(',');
            builder.Append(FormatNumber(result.ProcessingTimeMs));
            builder.Append(',');
            builder.Append(FormatNumber(result.PeakWorkingSetMb));
            builder.Append(',');
            builder.Append(FormatNumber(result.Rtf));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatJsonl(IReadOnlyList<BenchmarkResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            builder.AppendLine(JsonSerializer.Serialize(new
            {
                configuration_id = result.Configuration.Id,
                keywords_file = result.Configuration.KeywordsFile,
                score = result.Configuration.Score,
                threshold = result.Configuration.Threshold,
                clip = result.CorpusClip.RelativePath,
                label = result.CorpusClip.Label,
                tags = result.CorpusClip.Tags,
                outcome = BenchmarkFormatting.OutcomeName(result.Outcome),
                audio_duration_ms = Round(result.AudioDurationMs),
                detection_count = result.DetectionCount,
                first_detection_offset_ms = RoundNullable(result.FirstDetectionOffsetMs),
                latency_ms = RoundNullable(result.LatencyMs),
                processing_time_ms = Round(result.ProcessingTimeMs),
                rtf = Round(result.Rtf),
            }));
        }

        return builder.ToString();
    }

    private static string FormatReport(
        BenchmarkOptions options,
        CorpusManifest manifest,
        IReadOnlyList<BenchmarkResult> results,
        BenchmarkSummary summary,
        DateTime timestamp)
    {
        var ranked = summary.Rank();
        var positiveCount = manifest.Clips.Count(
            clip => string.Equals(clip.Label, "positive", StringComparison.OrdinalIgnoreCase));
        var negativeCount = manifest.Clips.Count - positiveCount;
        var negativeHours = ranked.FirstOrDefault()?.NegativeHours ?? 0.0;

        var builder = new StringBuilder();
        builder.AppendLine("# Wake-word benchmark report");
        builder.AppendLine();
        builder.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"- **Date:** {timestamp:yyyy-MM-dd HH:mm:ss}"));
        builder.AppendLine($"- **Manifest:** {options.ManifestPath}");
        builder.AppendLine($"- **Model:** {options.ModelPath}");
        builder.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"- **Corpus:** {manifest.Clips.Count} clips — {positiveCount} positive," +
                $" {negativeCount} negative ({negativeHours:0.###} h of negative audio)"));
        builder.AppendLine(
            $"- **Processing:** {options.ProcessingSampleRate} Hz, chunk {options.ChunkBytes} B," +
            $" tail {options.TailMs} ms");
        if (ranked.Count > 0 && ranked.All(row => row.AverageLatencyMs is null))
        {
            builder.AppendLine("- **Latency:** unavailable (manifest clips have no `wakeWordEndMs`)");
        }

        builder.AppendLine();
        builder.AppendLine("## Leaderboard");
        builder.AppendLine();
        AppendMarkdownLeaderboard(builder, ranked);
        builder.AppendLine();
        builder.AppendLine("## Failures by configuration");

        foreach (var row in ranked)
        {
            var configurationResults = results
                .Where(result => result.Configuration.Id == row.ConfigurationId)
                .ToArray();
            var missed = configurationResults
                .Where(result => result.Outcome == ClipOutcome.Miss)
                .ToArray();
            var falseActivations = configurationResults
                .Where(result => result.Outcome == ClipOutcome.FalseActivation)
                .ToArray();

            builder.AppendLine();
            builder.AppendLine(
                $"### {Path.GetFileName(row.KeywordsFile)} —" +
                $" score {row.Score.ToString("0.0##", CultureInfo.InvariantCulture)}," +
                $" threshold {row.Threshold.ToString("0.0##", CultureInfo.InvariantCulture)}");
            builder.AppendLine();
            AppendMarkdownClipList(
                builder,
                "Missed",
                missed,
                _ => "no detection");
            builder.AppendLine();
            AppendMarkdownClipList(
                builder,
                "False activations",
                falseActivations,
                result => string.Create(
                    CultureInfo.InvariantCulture,
                    $"fired @ {result.FirstDetectionOffsetMs:0} ms"));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendMarkdownLeaderboard(
        StringBuilder builder,
        IReadOnlyList<BenchmarkSummaryRow> ranked)
    {
        builder.AppendLine("| Rank | Config | Score | Threshold | Recall | FA/h | Latency avg/p50 | RTF | Peak mem |");
        builder.AppendLine("| ---- | ------ | ----- | --------- | ------ | ---- | --------------- | --- | -------- |");
        for (var index = 0; index < ranked.Count; index++)
        {
            var row = ranked[index];
            var recall = row.Recall is null
                ? "-"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{row.DetectedCount}/{row.PositiveCount} ({row.Recall.Value * 100.0:0.0}%)");
            var cells = new[]
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                Path.GetFileName(row.KeywordsFile),
                row.Score.ToString("0.0##", CultureInfo.InvariantCulture),
                row.Threshold.ToString("0.0##", CultureInfo.InvariantCulture),
                recall,
                row.FalseActivationsPerHour?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-",
                BenchmarkFormatting.FormatLatency(row),
                row.Rtf.ToString("0.000", CultureInfo.InvariantCulture),
                row.PeakWorkingSetMb.ToString("0", CultureInfo.InvariantCulture) + " MB",
            };
            var formatted = index == 0 ? cells.Select(cell => $"**{cell}**") : cells;
            builder.AppendLine("| " + string.Join(" | ", formatted) + " |");
        }
    }

    private static void AppendMarkdownClipList(
        StringBuilder builder,
        string title,
        IReadOnlyList<BenchmarkResult> clips,
        Func<BenchmarkResult, string> describe)
    {
        if (clips.Count == 0)
        {
            builder.AppendLine($"{title}: none");
            return;
        }

        builder.AppendLine($"{title} ({clips.Count}):");
        foreach (var clip in clips.Take(MaxListedClipsPerSection))
        {
            builder.AppendLine($"- `{clip.CorpusClip.RelativePath}` — {describe(clip)}");
        }

        if (clips.Count > MaxListedClipsPerSection)
        {
            builder.AppendLine($"- … and {clips.Count - MaxListedClipsPerSection} more");
        }
    }

    private static double Round(double value) => Math.Round(value, 4);

    private static double? RoundNullable(double? value) =>
        value is null ? null : Math.Round(value.Value, 4);

    private static string FormatNumber(object? value)
    {
        return value switch
        {
            null => string.Empty,
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"Unsupported value type '{value.GetType().Name}'."),
        };
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
