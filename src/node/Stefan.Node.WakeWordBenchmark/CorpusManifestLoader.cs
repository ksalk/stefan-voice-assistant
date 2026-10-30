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

            var fileName = Path.GetFullPath(Path.Combine(manifestDirectory, relativePath));
            if (fileName.StartsWith(manifestDirectory, StringComparison.Ordinal) is false)
            {
                throw new InvalidDataException($"Manifest clip path is not under the manifest root: {relativePath}");
            }

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Manifest clip not found: {relativePath}", fileName);
            }

            if (seenClips.Contains(fileName))
            {
                continue;
            }
            seenClips.Add(fileName);

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

            var wakeWordEndMs = clipElement.TryGetProperty("wakeWordEndMs", out var wakeWordEndElement) &&
                wakeWordEndElement.ValueKind == JsonValueKind.Number
                ? wakeWordEndElement.GetDouble()
                : (double?)null;

            clips.Add(new CorpusClip(fileName, relativePath, label, wakeWordEndMs, tags));
        }

        return new CorpusManifest(clips);
    }
}
