using System.Text.Json;

namespace Stefan.Node.WakeWordBenchmark;

public static class CorpusManifestLoader
{
    public static CorpusManifest Load(string manifestPath)
    {
        var text = File.ReadAllText(manifestPath);
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (!document.RootElement.TryGetProperty("clips", out var clipsElement) || clipsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Manifest must contain an array property 'clips': '{manifestPath}'.");
        }

        var manifestDirectory = Path.GetFullPath(Path.GetDirectoryName(manifestPath)!);
        var clips = new List<CorpusClip>();
        var seenClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var clipElement in clipsElement.EnumerateArray())
        {
            var relativePath = clipElement.GetProperty("path").GetString()
                ?? throw new InvalidDataException($"Manifest clip is missing 'path': {clipElement.GetRawText()}");
            var label = clipElement.GetProperty("label").GetString()
                ?? throw new InvalidDataException($"Manifest clip is missing 'label': {clipElement.GetRawText()}");

            if (string.IsNullOrWhiteSpace(relativePath) ||
                string.IsNullOrWhiteSpace(label) ||
                (label != "positive" && label != "negative"))
            {
                throw new InvalidDataException($"Manifest clip is invalid: {clipElement.GetRawText()}");
            }

            var fullPath = Path.GetFullPath(Path.Combine(manifestDirectory, relativePath));
            if (fullPath.StartsWith(manifestDirectory, StringComparison.Ordinal) is false)
            {
                throw new InvalidDataException($"Manifest clip path is not under the manifest root: {relativePath}");
            }

            var tags = ParseTags(clipElement);
            var wakeWordEndMs = ParseWakeWordEndMs(clipElement);

            if (Directory.Exists(fullPath))
            {
                // A directory expands to every .wav file below it, recursively.
                var files = Directory
                    .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                    .Where(IsWavFile)
                    .OrderBy(file => file, StringComparer.Ordinal)
                    .ToArray();
                if (files.Length == 0)
                {
                    throw new InvalidDataException($"Manifest directory contains no .wav files: {relativePath}");
                }

                foreach (var file in files)
                {
                    AddClip(clips, seenClips, file, Path.GetRelativePath(manifestDirectory, file), label, wakeWordEndMs, tags);
                }
            }
            else if (File.Exists(fullPath))
            {
                AddClip(clips, seenClips, fullPath, relativePath, label, wakeWordEndMs, tags);
            }
            else
            {
                throw new FileNotFoundException($"Manifest clip not found: {relativePath}", fullPath);
            }
        }

        return new CorpusManifest(clips);
    }

    private static bool IsWavFile(string path) =>
        string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase);

    private static void AddClip(
        List<CorpusClip> clips,
        HashSet<string> seenClips,
        string fileName,
        string relativePath,
        string label,
        double? wakeWordEndMs,
        IReadOnlyList<string> tags)
    {
        if (seenClips.Add(fileName))
        {
            clips.Add(new CorpusClip(fileName, relativePath, label, wakeWordEndMs, tags));
        }
    }

    private static List<string> ParseTags(JsonElement clipElement)
    {
        var tags = new List<string>();
        if (clipElement.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsElement.EnumerateArray())
            {
                var tagText = tag.GetString();
                if (!string.IsNullOrWhiteSpace(tagText))
                {
                    tags.Add(tagText);
                }
            }
        }

        return tags;
    }

    private static double? ParseWakeWordEndMs(JsonElement clipElement) =>
        clipElement.TryGetProperty("wakeWordEndMs", out var wakeWordEndElement) &&
        wakeWordEndElement.ValueKind == JsonValueKind.Number
            ? wakeWordEndElement.GetDouble()
            : null;
}
