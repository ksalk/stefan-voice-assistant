using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Stefan.Server.SttBenchmarks.Config;
using Whisper.net;

namespace Stefan.Server.SttBenchmarks.Benchmarks;

[Config(typeof(SttBenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class WhisperSpeedBenchmark : IDisposable
{
    private static string ModelsDir =>
        Environment.GetEnvironmentVariable("STT_BENCHMARK_MODELS_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "stt-models"));

    private static readonly (string Name, string File)[] Models =
    [
        ("base", "ggml-base.en.bin"),
        ("small", "ggml-small.en.bin"),
        ("small-q8", "ggml-small.en-q8_0.bin"),
        ("medium", "ggml-medium.bin"),
        ("large-turbo-q5", "ggml-large-v3-turbo-q5_0.bin"),
    ];

    private readonly Dictionary<string, WhisperProcessor> _processors = new();
    private byte[] _audioData = null!;

    [ParamsSource(nameof(ModelNames))]
    public string Model { get; set; } = null!;

    public IEnumerable<string> ModelNames => Models.Select(m => m.Name);

    [GlobalSetup]
    public void Setup()
    {
        var audioDir = Environment.GetEnvironmentVariable("STT_BENCHMARK_AUDIO_DIR")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestAudio"));
        var audioFile = FindAudioFile(audioDir);

        if (audioFile is null)
            throw new FileNotFoundException($"No audio file found in {audioDir}");

        _audioData = File.ReadAllBytes(audioFile);

        foreach (var (name, file) in Models)
        {
            var modelPath = Path.Combine(ModelsDir, file);
            if (!File.Exists(modelPath))
                continue;

            var factory = WhisperFactory.FromPath(modelPath);
            var processor = factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
            _processors[name] = processor;
        }
    }

    [Benchmark]
    public async Task<string> Whisper_Transcribe()
    {
        using var stream = new MemoryStream(_audioData);
        var processor = _processors[Model];

        var segments = new List<string>();
        await foreach (var segment in processor.ProcessAsync(stream))
            segments.Add(segment.Text);

        return string.Concat(segments).Trim();
    }

    public void Dispose()
    {
        foreach (var processor in _processors.Values)
            processor.Dispose();
    }

    private static string? FindAudioFile(string dir)
    {
        if (!Directory.Exists(dir))
            return null;

        return Directory.GetFiles(dir, "*.wav").FirstOrDefault();
    }
}
