using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Stefan.Server.SttBenchmarks.Config;
using Vosk;

namespace Stefan.Server.SttBenchmarks.Benchmarks;

[Config(typeof(SttBenchmarkConfig))]
public class VoskSpeedBenchmark : IDisposable
{
    private const int BufferSize = 4096;

    private static string ModelsDir =>
        Environment.GetEnvironmentVariable("STT_BENCHMARK_MODELS_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "stt-models"));

    private readonly Dictionary<string, Model> _models = new();
    private byte[] _audioData = null!;

    [ParamsSource(nameof(VoskModelNames))]
    public string Model { get; set; } = null!;

    public IEnumerable<string> VoskModelNames => DiscoverModels().Select(m => m.Name);

    [GlobalSetup]
    public void Setup()
    {
        Vosk.Vosk.SetLogLevel(-1);

        foreach (var (name, path) in DiscoverModels())
        {
            _models[name] = new Model(path);
        }

        var audioDir = Environment.GetEnvironmentVariable("STT_BENCHMARK_AUDIO_DIR")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestAudio"));
        var audioFile = FindAudioFile(audioDir);

        if (audioFile is null)
            throw new FileNotFoundException($"No audio file found in {audioDir}");

        _audioData = File.ReadAllBytes(audioFile);
    }

    [Benchmark]
    public string Vosk_Transcribe()
    {
        using var rec = new VoskRecognizer(_models[Model], 16000.0f);
        rec.SetMaxAlternatives(0);
        rec.SetWords(false);

        using var stream = new MemoryStream(_audioData);
        var buffer = new byte[BufferSize];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            rec.AcceptWaveform(buffer, bytesRead);

        var resultJson = rec.FinalResult();
        using var doc = JsonDocument.Parse(resultJson);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
            model.Dispose();
    }

    internal static List<(string Name, string Path)> DiscoverModels()
    {
        if (!Directory.Exists(ModelsDir))
            return [];

        return Directory.GetDirectories(ModelsDir, "vosk-model-*")
            .Select(path => (Name: Path.GetFileName(path), Path: path))
            .OrderBy(m => m.Name)
            .ToList();
    }

    private static string? FindAudioFile(string dir)
    {
        if (!Directory.Exists(dir))
            return null;

        return Directory.GetFiles(dir, "*.wav").FirstOrDefault();
    }
}
