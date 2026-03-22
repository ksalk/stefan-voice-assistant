using BenchmarkDotNet.Running;
using Stefan.Server.SttBenchmarks.Benchmarks;
using Stefan.Server.SttBenchmarks.Data;

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var engine = args.Length > 2 && args[1] == "--engine" ? args[2].ToLowerInvariant() : null;

var audioDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestAudio"));
var modelsDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "stt-models"));

Environment.SetEnvironmentVariable("STT_BENCHMARK_AUDIO_DIR", audioDir);
Environment.SetEnvironmentVariable("STT_BENCHMARK_MODELS_DIR", modelsDir);

switch (mode)
{
    case "speed":
        RunSpeedBenchmarks(engine);
        break;
    case "quality":
        RunQualityBenchmark(engine, audioDir);
        break;
    default:
        Console.WriteLine("Usage: dotnet run -- [speed|quality] [--engine whisper|vosk]");
        Console.WriteLine();
        Console.WriteLine("  speed    Run BenchmarkDotNet speed benchmarks");
        Console.WriteLine("  quality  Run WER/CER quality comparison across engines");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --engine <name>  Only benchmark a specific engine (whisper, vosk)");
        break;
}

void RunSpeedBenchmarks(string? filter)
{
    if (filter is null)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return;
    }

    Type[] types = filter switch
    {
        "whisper" => [typeof(WhisperSpeedBenchmark)],
        "vosk" => [typeof(VoskSpeedBenchmark)],
        _ => throw new ArgumentException($"Unknown engine: {filter}")
    };

    foreach (var type in types)
        BenchmarkRunner.Run(type);
}

void RunQualityBenchmark(string? filter, string audioDirectory)
{
    var testCases = TestCaseLoader.Load(audioDirectory);

    if (testCases.Count == 0)
    {
        Console.WriteLine($"No test cases found in {audioDirectory}");
        Console.WriteLine("Add a test_cases.json file and audio files to the TestAudio directory.");
        return;
    }

    Console.WriteLine($"Loaded {testCases.Count} test case(s) from {audioDirectory}");
    Console.WriteLine();

    var benchmark = new QualityBenchmark();
    benchmark.Run(testCases, audioDirectory, filter);
}
