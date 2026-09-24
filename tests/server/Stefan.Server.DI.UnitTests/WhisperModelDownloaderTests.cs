using System.Net;
using System.Net.Sockets;
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

        using (var server = new FakeModelServer(payload, truncate: false))
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

        using (var server = new FakeModelServer(payload, truncate: true))
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

        using (var server = new FakeModelServer(payload, truncate: false))
        {
            downloader.EnsureModel(filePath, server.Url);
        }

        Assert.Equal(payload, File.ReadAllBytes(filePath));
    }

    private sealed class FakeModelServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _worker;

        public string Url { get; }

        public FakeModelServer(byte[] payload, bool truncate)
        {
            var port = ReserveFreePort();
            Url = $"http://127.0.0.1:{port}/model.bin";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _worker = Task.Run(async () =>
            {
                var context = await _listener.GetContextAsync();
                context.Response.ContentLength64 = payload.Length;
                var length = truncate ? payload.Length / 2 : payload.Length;
                await context.Response.OutputStream.WriteAsync(payload, 0, length);
                if (truncate)
                {
                    context.Response.Abort();
                }
                else
                {
                    context.Response.Close();
                }
            });
        }

        private static int ReserveFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
            }

            try
            {
                _worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
        }
    }
}
