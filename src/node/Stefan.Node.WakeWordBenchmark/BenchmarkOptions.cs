using System.Globalization;
using System.Text.Json;
using Stefan.Node.Options;

namespace Stefan.Node.WakeWordBenchmark;

public sealed record EnsembleMember(string KeywordsFile, float Score, float Threshold);

public sealed record BenchmarkOptions(
    string ManifestPath,
    string OutputDirectory,
    string ModelPath,
    IReadOnlyList<string> KeywordFiles,
    IReadOnlyList<EnsembleMember> EnsembleMembers,
    IReadOnlyList<string> KeywordTranscriptions,
    IReadOnlyList<float> Scores,
    IReadOnlyList<float> Thresholds,
    IReadOnlyList<int> MaxActivePaths,
    IReadOnlyList<int> NumTrailingBlanks,
    string EncoderFile,
    string DecoderFile,
    string JoinerFile,
    int ChunkBytes,
    int TailMs,
    int Threads,
    int ProcessingSampleRate,
    bool NoResample,
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
        var maxActivePaths = parser.GetIntList("max-active-paths", "4");
        var numTrailingBlanks = parser.GetIntList("num-trailing-blanks", "1");
        var encoderFile = parser.GetString("encoder-file", "encoder-epoch-13-avg-2-chunk-16-left-64.onnx");
        var decoderFile = parser.GetString("decoder-file", "decoder-epoch-13-avg-2-chunk-16-left-64.onnx");
        var joinerFile = parser.GetString("joiner-file", "joiner-epoch-13-avg-2-chunk-16-left-64.onnx");
        var ensembleMembers = LoadEnsemble(parser.GetString("ensemble", null));
        var noResample = ParserHasFlag(args, "no-resample");

        var keywordSpotterOptions = new KeywordSpotterOptions
        {
            ModelPath = modelPath,
            NumThreads = threads,
            Provider = "cpu",
            FeatureDim = 80,
            KeywordsScore = scores.First(),
            KeywordsThreshold = thresholds.First(),
            EncoderFile = encoderFile,
            DecoderFile = decoderFile,
            JoinerFile = joinerFile,
            MaxActivePaths = maxActivePaths.First(),
            NumTrailingBlanks = numTrailingBlanks.First()
        };

        if (ensembleMembers.Count > 0)
        {
            // Ensemble mode: every member provides its own keywords file, score and threshold.
            var groups = new List<KeywordGroupOptions>();
            foreach (var member in ensembleMembers)
            {
                groups.Add(new KeywordGroupOptions
                {
                    KeywordsFile = member.KeywordsFile,
                    KeywordsScore = member.Score,
                    KeywordsThreshold = member.Threshold,
                });
            }
            keywordSpotterOptions = new KeywordSpotterOptions
            {
                ModelPath = modelPath,
                NumThreads = threads,
                Provider = "cpu",
                FeatureDim = 80,
                EncoderFile = encoderFile,
                DecoderFile = decoderFile,
                JoinerFile = joinerFile,
                MaxActivePaths = maxActivePaths.First(),
                NumTrailingBlanks = numTrailingBlanks.First(),
                Groups = groups.ToArray(),
            };
        }

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
            ensembleMembers,
            keywordTranscriptions,
            scores,
            thresholds,
            maxActivePaths,
            numTrailingBlanks,
            encoderFile,
            decoderFile,
            joinerFile,
            chunkBytes,
            tailMs,
            threads,
            processingSampleRate,
            noResample,
            keywordSpotterOptions,
            audioOptions);
    }

    private static bool ParserHasFlag(string[] args, string name)
    {
        var flag = $"--{name}";
        return args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<EnsembleMember> LoadEnsemble(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var resolved = File.Exists(path) ? Path.GetFullPath(path) : path;
        var json = File.ReadAllText(resolved);
        var members = JsonSerializer.Deserialize<List<EnsembleMember>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidDataException($"Ensemble file '{resolved}' is empty.");

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (string.IsNullOrWhiteSpace(member.KeywordsFile))
            {
                throw new InvalidDataException($"Ensemble member {i} has empty keywordsFile: '{resolved}'.");
            }
            var keywordsPath = File.Exists(member.KeywordsFile)
                ? Path.GetFullPath(member.KeywordsFile)
                : Path.GetFullPath(Path.Join(Path.GetDirectoryName(resolved)!, member.KeywordsFile));
            members[i] = member with { KeywordsFile = keywordsPath };
        }

        return members;
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
