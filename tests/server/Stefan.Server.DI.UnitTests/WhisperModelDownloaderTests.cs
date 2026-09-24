using Microsoft.Extensions.Logging.Abstractions;
using Stefan.Server.Application.Services;

namespace Stefan.Server.DI.UnitTests;

public class WhisperModelDownloaderTests : IDisposable
{
    private readonly string _tempDir;

    public WhisperModelDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whisper-downloader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void EnsureModel_WhenFileAlreadyExists_DoesNotDownload()
    {
        var filePath = Path.Combine(_tempDir, "ggml-base.bin");
        File.WriteAllText(filePath, "existing");
        var downloader = new WhisperModelDownloader(NullLogger<WhisperModelDownloader>.Instance);

        downloader.EnsureModel(filePath, "http://127.0.0.1:1/ggml-base.bin");

        Assert.Equal("existing", File.ReadAllText(filePath));
    }

    [Fact]
    public void EnsureModel_WhenFileMissing_DownloadsAndWritesFile()
    {
        var payload = "fake-whisper-model"u8.ToArray();
        var filePath = Path.Combine(_tempDir, "ggml-base.bin");
        var downloader = new WhisperModelDownloader(NullLogger<WhisperModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(payload, truncate: false))
        {
            downloader.EnsureModel(filePath, server.Url);
        }

        Assert.Equal(payload, File.ReadAllBytes(filePath));
        Assert.False(File.Exists(filePath + ".tmp"));
    }

    [Fact]
    public void EnsureModel_WhenDownloadFails_ThrowsAndLeavesNoPartialFiles()
    {
        var payload = "fake-whisper-model"u8.ToArray();
        var filePath = Path.Combine(_tempDir, "ggml-base.bin");
        var downloader = new WhisperModelDownloader(NullLogger<WhisperModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(payload, truncate: true))
        {
            Assert.ThrowsAny<Exception>(() => downloader.EnsureModel(filePath, server.Url));
        }

        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(filePath + ".tmp"));
    }

    [Fact]
    public void EnsureModel_WhenFileMissing_CreatesMissingDirectories()
    {
        var payload = "fake-whisper-model"u8.ToArray();
        var filePath = Path.Combine(_tempDir, "nested", "dir", "ggml-base.bin");
        var downloader = new WhisperModelDownloader(NullLogger<WhisperModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(payload, truncate: false))
        {
            downloader.EnsureModel(filePath, server.Url);
        }

        Assert.Equal(payload, File.ReadAllBytes(filePath));
    }
}
