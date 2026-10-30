using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stefan.Node.WakeWordBenchmark;

public static class BenchmarkReportWriter
{
    public static void Write(
        BenchmarkOptions options,
        IReadOnlyList<BenchmarkResult> results,
        BenchmarkSummary summary)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "summary.csv"), FormatSummary(summary));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "results.csv"), FormatResults(results));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "configuration.json"), JsonSerializer.Serialize(new
        {
            manifest_path = options.ManifestPath,
            output_directory = options.OutputDirectory,
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
        var builder = new StringBuilder("configuration_id,keywords_file,score,threshold,clip_path,label,audio_duration_ms,detection_count,first_detection_offset_ms,processing_time_ms,peak_working_set_mb,rtf\n");
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
            builder.Append(FormatNumber(result.AudioDurationMs));
            builder.Append(',');
            builder.Append(FormatNumber(result.DetectionCount));
            builder.Append(',');
            builder.Append(FormatNumber(result.FirstDetectionOffsetMs));
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
