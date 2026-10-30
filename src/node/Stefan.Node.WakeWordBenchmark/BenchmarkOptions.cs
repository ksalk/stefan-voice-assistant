using System.Globalization;
using Stefan.Node.Options;

namespace Stefan.Node.WakeWordBenchmark;

public sealed record BenchmarkOptions(
    string ManifestPath,
    string OutputDirectory,
    string ModelPath,
    IReadOnlyList<string> KeywordFiles,
    IReadOnlyList<string> KeywordTranscriptions,
    IReadOnlyList<float> Scores,
    IReadOnlyList<float> Thresholds,
    int ChunkBytes,
    int TailMs,
    int Threads,
    int ProcessingSampleRate,
    KeywordSpotterOptions KeywordSpotterOptions,
    AudioOptions AudioOptions)
{
    public static BenchmarkOptions CreateFromArguments(string[] args)
    {
        var parser = new ArgumentParser(args);

        var manifestPath = parser.RequireString("manifest");
        manifestPath = File.Exists(manifestPath) ? Path.GetFullPath(manifestPath) : manifestPath;
        var modelPath = parser.GetString("model-path", "/app/models");
        var keywordFiles = parser.GetStringList("keywords-file", "src/node/Stefan.Node/keywords.txt")
            .Select(path => File.Exists(path) ? Path.GetFullPath(path) : path)
            .ToArray();
        var outputDirectory = parser.GetString("output-dir", Path.Combine(Path.GetDirectoryName(manifestPath)!, "results"));
        var keywordTranscriptions = parser.GetStringList("keyword-transcription", null);
        var scores = parser.GetFloatList("scores", "2.0");
        var thresholds = parser.GetFloatList("thresholds", "0.2");
        var chunkBytes = parser.GetInt("chunk-bytes", 4096);
        var tailMs = parser.GetInt("tail-ms", 600);
        var threads = parser.GetInt("threads", 2);
        var processingSampleRate = parser.GetInt("processing-sample-rate", 16000);

        var keywordSpotterOptions = new KeywordSpotterOptions
        {
            ModelPath = modelPath,
            NumThreads = threads,
            Provider = "cpu",
            FeatureDim = 80,
            KeywordsScore = scores.First(),
            KeywordsThreshold = thresholds.First()
        };

        var audioOptions = new AudioOptions
        {
            Input = new AudioInputOptions
            {
                DeviceName = "null",
                SampleRate = processingSampleRate,
                Channels = 1,
                BitsPerSample = 16,
                ProcessingSampleRate = processingSampleRate,
            },
            Output = new AudioOutputOptions
            {
                DeviceName = "null",
                VolumeControlName = "Master",
            },
        };

        return new BenchmarkOptions(
            manifestPath,
            outputDirectory,
            modelPath,
            keywordFiles,
            keywordTranscriptions,
            scores,
            thresholds,
            chunkBytes,
            tailMs,
            threads,
            processingSampleRate,
            keywordSpotterOptions,
            audioOptions);
    }

    private sealed class ArgumentParser(string[] args)
    {
        public string RequireString(string name)
        {
            if (!string.Equals(name, "manifest", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid argument '{name}'.");
            }

            var value = GetString("manifest", null);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required argument '--{name}'.");
            }

            return value;
        }

        public string GetString(string name, string? defaultValue) =>
            GetValues(name).FirstOrDefault() ?? defaultValue ?? string.Empty;

        public IReadOnlyList<string> GetStringList(string name, string? defaultValue)
        {
            var parts = new List<string>();
            foreach (var value in GetValues(name))
            {
                foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    parts.Add(item);
                }
            }

            if (parts.Count == 0 && defaultValue is not null)
            {
                parts.Add(defaultValue);
            }

            return parts;
        }

        public IReadOnlyList<float> GetFloatList(string name, string? defaultValue)
        {
            var parts = new List<float>();
            foreach (var value in GetValues(name))
            {
                foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    parts.Add(float.Parse(item, CultureInfo.InvariantCulture));
                }
            }

            if (parts.Count == 0 && defaultValue is not null)
            {
                parts.Add(float.Parse(defaultValue, CultureInfo.InvariantCulture));
            }

            return parts;
        }

        public IReadOnlyList<int> GetIntList(string name, string? defaultValue)
        {
            var parts = new List<int>();
            foreach (var value in GetValues(name))
            {
                foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    parts.Add(int.Parse(item, CultureInfo.InvariantCulture));
                }
            }

            if (parts.Count == 0 && defaultValue is not null)
            {
                parts.Add(int.Parse(defaultValue, CultureInfo.InvariantCulture));
            }

            return parts;
        }

        public int GetInt(string name, int defaultValue)
        {
            var value = GetValues(name).FirstOrDefault();
            return value is null ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
        }

        private IEnumerable<string> GetValues(string name)
        {
            var argument = $"--{name}";
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(argument, args[i], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException($"Missing value for argument '{argument}'.");
                }

                yield return args[++i];
            }
        }
    }
}
