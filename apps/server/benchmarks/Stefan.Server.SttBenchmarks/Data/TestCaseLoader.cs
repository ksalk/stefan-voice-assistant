using System.Text.Json;

namespace Stefan.Server.SttBenchmarks.Data;

public static class TestCaseLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<AudioTestCase> Load(string audioDirectory)
    {
        var configPath = Path.Combine(audioDirectory, "test_cases.json");

        if (!File.Exists(configPath))
            return [];

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<List<AudioTestCase>>(json, JsonOptions) ?? [];
    }
}
