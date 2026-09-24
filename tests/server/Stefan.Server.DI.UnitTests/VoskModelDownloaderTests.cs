using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Stefan.Server.Application.Services;

namespace Stefan.Server.DI.UnitTests;

public class VoskModelDownloaderTests : IDisposable
{
    private const string ZipTopLevelDir = "vosk-model-en-us-0.22";

    private readonly string _tempDir;

    public VoskModelDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vosk-downloader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void EnsureModel_WhenModelDirectoryAlreadyExists_DoesNotDownload()
    {
        var modelPath = Path.Combine(_tempDir, "vosk-model-en-us-0.22");
        Directory.CreateDirectory(modelPath);
        File.WriteAllText(Path.Combine(modelPath, "model.json"), "{}");
        var downloader = new VoskModelDownloader(NullLogger<VoskModelDownloader>.Instance);

        downloader.EnsureModel(modelPath, "http://127.0.0.1:1/model.zip");

        Assert.Equal("{}", File.ReadAllText(Path.Combine(modelPath, "model.json")));
    }

    [Fact]
    public void EnsureModel_WhenModelMissing_DownloadsExtractsAndMoves()
    {
        var zipPayload = BuildModelZip();
        var modelPath = Path.Combine(_tempDir, "vosk-model-en-us-0.22");
        var downloader = new VoskModelDownloader(NullLogger<VoskModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(zipPayload))
        {
            downloader.EnsureModel(modelPath, server.Url);
        }

        Assert.True(File.Exists(Path.Combine(modelPath, "am", "final.mdl")));
        Assert.False(File.Exists(modelPath + ".zip.tmp"));
        Assert.Empty(Directory.GetDirectories(_tempDir, ".vosk-extract-*"));
    }

    [Fact]
    public void EnsureModel_WhenConfiguredPathDiffersFromZip_RenamesExtractedDirectory()
    {
        var zipPayload = BuildModelZip();
        var modelPath = Path.Combine(_tempDir, "vosk-model-en-us-0.42");
        var downloader = new VoskModelDownloader(NullLogger<VoskModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(zipPayload))
        {
            downloader.EnsureModel(modelPath, server.Url);
        }

        Assert.True(Directory.Exists(modelPath));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, ZipTopLevelDir)));
    }

    [Fact]
    public void EnsureModel_WhenDownloadFails_ThrowsAndLeavesNoPartialFiles()
    {
        var zipPayload = BuildModelZip();
        var modelPath = Path.Combine(_tempDir, "vosk-model-en-us-0.22");
        var downloader = new VoskModelDownloader(NullLogger<VoskModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(zipPayload, truncate: true))
        {
            Assert.ThrowsAny<Exception>(() => downloader.EnsureModel(modelPath, server.Url));
        }

        Assert.False(Directory.Exists(modelPath));
        Assert.False(File.Exists(modelPath + ".zip.tmp"));
        Assert.Empty(Directory.GetDirectories(_tempDir, ".vosk-extract-*"));
    }

    [Fact]
    public void EnsureModel_WhenParentDirectoriesMissing_CreatesThem()
    {
        var zipPayload = BuildModelZip();
        var modelPath = Path.Combine(_tempDir, "data", "vosk", ZipTopLevelDir);
        var downloader = new VoskModelDownloader(NullLogger<VoskModelDownloader>.Instance);

        using (var server = FakeModelServer.Start(zipPayload))
        {
            downloader.EnsureModel(modelPath, server.Url);
        }

        Assert.True(File.Exists(Path.Combine(modelPath, "am", "final.mdl")));
    }

    private static byte[] BuildModelZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entryPath in new[]
                     {
                         $"{ZipTopLevelDir}/model.json",
                         $"{ZipTopLevelDir}/am/final.mdl",
                         $"{ZipTopLevelDir}/conf/model.conf"
                     })
            {
                var entry = archive.CreateEntry(entryName: entryPath);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("fake-vosk-model-file");
            }
        }

        stream.Position = 0;
        return stream.ToArray();
    }
}
