using System.Net;
using Microsoft.Extensions.Logging;

namespace Stefan.Server.Application.Services;

public class WhisperModelDownloader(ILogger<WhisperModelDownloader> logger, HttpMessageHandler? handler = null)
{
    public const string DefaultModelUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";

    private readonly HttpClient _client = handler is null ? new HttpClient() : new HttpClient(handler);

    public void EnsureModel(string filePath, string downloadUrl = DefaultModelUrl)
    {
        if (File.Exists(filePath))
        {
            logger.LogInformation("Whisper model already present at {FilePath}.", filePath);
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        logger.LogInformation("Downloading Whisper model from {Url} to {FilePath}...", downloadUrl, filePath);
        var tempPath = filePath + ".tmp";
        try
        {
            using var response = _client
                .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter()
                .GetResult();
            response.EnsureSuccessStatusCode();

            using var contentStream = response.Content.ReadAsStream();
            using var fileStream = File.Create(tempPath);
            contentStream.CopyTo(fileStream);

            File.Move(tempPath, filePath);
        }
        catch (Exception)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        logger.LogInformation("Whisper model downloaded to {FilePath}.", filePath);
    }
}
