using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Stefan.Server.Application.Services;

public class VoskModelDownloader(ILogger<VoskModelDownloader> logger, HttpMessageHandler? handler = null)
{
    public const string DefaultModelUrl = "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip";

    private readonly HttpClient _client = handler is null ? new HttpClient() : new HttpClient(handler);

    public void EnsureModel(string modelPath, string downloadUrl = DefaultModelUrl)
    {
        if (Directory.Exists(modelPath) && Directory.EnumerateFileSystemEntries(modelPath).Any())
        {
            logger.LogInformation("Vosk model already present at {ModelPath}.", modelPath);
            return;
        }

        var parentDir = Path.GetDirectoryName(Path.GetFullPath(modelPath));
        if (parentDir is not null && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        var tempZipPath = Path.Combine(parentDir!, $".{Path.GetFileName(Path.GetFullPath(modelPath))}.zip.tmp");
        var tempExtractDir = Path.Combine(parentDir!, $".vosk-extract-{Guid.NewGuid():N}");

        logger.LogInformation("Downloading Vosk model from {Url} to {ModelPath}...", downloadUrl, modelPath);
        try
        {
            using (var response = _client
                       .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();

                using var contentStream = response.Content.ReadAsStream();
                using var fileStream = File.Create(tempZipPath);
                contentStream.CopyTo(fileStream);
            }

            ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir);

            var extracted = Directory.GetDirectories(tempExtractDir);
            if (extracted.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected the Vosk model zip to contain exactly one top-level directory, but found {extracted.Length}.");
            }

            var target = Path.GetFullPath(modelPath);
            if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any())
            {
                Directory.Delete(target);
            }

            Directory.Move(extracted[0], target);
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            if (Directory.Exists(tempExtractDir))
            {
                Directory.Delete(tempExtractDir, recursive: true);
            }
        }

        logger.LogInformation("Vosk model downloaded to {ModelPath}.", modelPath);
    }
}
