using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Stefan.Server.SttBenchmarks.Data;
using Stefan.Server.SttBenchmarks.Metrics;
using Vosk;
using Whisper.net;

namespace Stefan.Server.SttBenchmarks.Benchmarks;

public sealed class QualityBenchmark
{
    private static string ModelsDir =>
        Environment.GetEnvironmentVariable("STT_BENCHMARK_MODELS_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "stt-models"));

    private static readonly (string Name, string File)[] WhisperModels =
    [
        ("whisper-base", "ggml-base.en.bin"),
        ("whisper-small", "ggml-small.en.bin"),
        ("whisper-small-q8", "ggml-small.en-q8_0.bin"),
        ("whisper-medium", "ggml-medium.bin"),
        ("whisper-large-turbo-q5", "ggml-large-v3-turbo-q5_0.bin"),
    ];

    public void Run(IReadOnlyList<AudioTestCase> testCases, string audioDirectory, string? engineFilter)
    {
        var results = new List<EngineResult>();
        var runWhisper = engineFilter is null or "whisper";
        var runVosk = engineFilter is null or "vosk";

        if (runWhisper)
        {
            foreach (var (name, file) in WhisperModels)
            {
                var modelPath = Path.Combine(ModelsDir, file);
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"Skipping {name}: model not found at {modelPath}");
                    continue;
                }

                Console.WriteLine($"Benchmarking {name}...");
                var result = BenchmarkWhisper(name, modelPath, testCases, audioDirectory);
                results.Add(result);
            }
        }

        if (runVosk)
        {
            var voskModels = VoskSpeedBenchmark.DiscoverModels();
            if (voskModels.Count > 0)
            {
                foreach (var (name, path) in voskModels)
                {
                    Console.WriteLine($"Benchmarking {name}...");
                    var result = BenchmarkVosk(name, path, testCases, audioDirectory);
                    results.Add(result);
                }
            }
            else
            {
                Console.WriteLine("Skipping Vosk: no vosk-model-* directories found in stt-models/");
            }
        }

        if (results.Count == 0)
        {
            Console.WriteLine("No engines were benchmarked.");
            return;
        }

        PrintResults(results);
    }

    private static EngineResult BenchmarkWhisper(
        string name, string modelPath,
        IReadOnlyList<AudioTestCase> testCases, string audioDirectory)
    {
        using var factory = WhisperFactory.FromPath(modelPath);
        using var processor = factory.CreateBuilder()
            .WithLanguage("en")
            .Build();

        var caseResults = new List<CaseResult>();
        var totalMs = 0.0;

        foreach (var testCase in testCases)
        {
            var audioPath = Path.Combine(audioDirectory, testCase.AudioFile);
            if (!File.Exists(audioPath))
            {
                Console.WriteLine($"  Skipping {testCase.AudioFile}: file not found");
                continue;
            }

            var audioData = File.ReadAllBytes(audioPath);
            var sw = Stopwatch.StartNew();

            var segments = new List<string>();
            using (var stream = new MemoryStream(audioData))
            {
                var enumerable = processor.ProcessAsync(stream);
                var enumerator = enumerable.GetAsyncEnumerator();
                while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                    segments.Add(enumerator.Current.Text);
            }

            sw.Stop();

            var transcript = string.Concat(segments).Trim();
            totalMs += sw.Elapsed.TotalMilliseconds;

            caseResults.Add(new CaseResult(
                testCase.AudioFile,
                testCase.ExpectedText,
                transcript,
                sw.Elapsed.TotalMilliseconds));
        }

        return new EngineResult(name, caseResults, totalMs);
    }

    private static EngineResult BenchmarkVosk(
        string name, string modelPath,
        IReadOnlyList<AudioTestCase> testCases, string audioDirectory)
    {
        Vosk.Vosk.SetLogLevel(-1);
        using var model = new Model(modelPath);

        var caseResults = new List<CaseResult>();
        var totalMs = 0.0;

        foreach (var testCase in testCases)
        {
            var audioPath = Path.Combine(audioDirectory, testCase.AudioFile);
            if (!File.Exists(audioPath))
            {
                Console.WriteLine($"  Skipping {testCase.AudioFile}: file not found");
                continue;
            }

            var audioData = File.ReadAllBytes(audioPath);
            var sw = Stopwatch.StartNew();

            using var rec = new VoskRecognizer(model, 16000.0f);
            rec.SetMaxAlternatives(0);
            rec.SetWords(false);

            using var stream = new MemoryStream(audioData);
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                rec.AcceptWaveform(buffer, bytesRead);

            var resultJson = rec.FinalResult();
            sw.Stop();

            using var doc = JsonDocument.Parse(resultJson);
            var transcript = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            totalMs += sw.Elapsed.TotalMilliseconds;

            caseResults.Add(new CaseResult(
                testCase.AudioFile,
                testCase.ExpectedText,
                transcript,
                sw.Elapsed.TotalMilliseconds));
        }

        return new EngineResult(name, caseResults, totalMs);
    }

    private static void PrintResults(List<EngineResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== Quality & Speed Results ===");
        Console.WriteLine();

        var separator = new string('-', 70);
        Console.WriteLine(string.Format("{0,-28} {1,8} {2,8} {3,10} {4,12}", "Engine", "Avg WER", "Avg CER", "Total ms", "Avg ms/file"));
        Console.WriteLine(separator);

        foreach (var r in results.OrderBy(r => r.TotalMs))
        {
            var avgWer = r.Cases.Count > 0 ? r.Cases.Average(c => c.Wer) : 0;
            var avgCer = r.Cases.Count > 0 ? r.Cases.Average(c => c.Cer) : 0;
            var avgMs = r.Cases.Count > 0 ? r.TotalMs / r.Cases.Count : 0;

            Console.WriteLine(string.Format("{0,-28} {1,8:P1} {2,8:P1} {3,10:F1} {4,12:F1}", r.Engine, avgWer, avgCer, r.TotalMs, avgMs));
        }

        Console.WriteLine();
        Console.WriteLine("=== Per-File Details ===");
        Console.WriteLine();

        foreach (var r in results)
        {
            Console.WriteLine($"--- {r.Engine} ---");
            foreach (var c in r.Cases)
            {
                Console.WriteLine($"  {c.AudioFile}:");
                Console.WriteLine($"    Expected:   \"{c.ExpectedText}\"");
                Console.WriteLine($"    Got:        \"{c.Transcript}\"");
                Console.WriteLine($"    WER: {c.Wer:P1}  CER: {c.Cer:P1}  Time: {c.ElapsedMs:F1}ms");
            }
            Console.WriteLine();
        }
    }

    private sealed record EngineResult(
        string Engine,
        List<CaseResult> Cases,
        double TotalMs);

    private sealed record CaseResult(
        string AudioFile,
        string ExpectedText,
        string Transcript,
        double ElapsedMs)
    {
        public double Wer { get; } = ErrorMetrics.WordErrorRate(ExpectedText, Transcript);
        public double Cer { get; } = ErrorMetrics.CharacterErrorRate(ExpectedText, Transcript);
    }
}
