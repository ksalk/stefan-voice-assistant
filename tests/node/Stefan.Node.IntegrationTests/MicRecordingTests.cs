using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class MicRecordingTests : IntegrationTestBase
{
    [Fact]
    public async Task RecordingEnabled_RecordsRawAndProcessedInput_WithoutWakeWordProcessing()
    {
        // Arrange — capture mode must replace wake-word and command processing completely.
        var hostRecordingsDir = Path.Combine(Path.GetTempPath(), "stefan-node-tests", Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(hostRecordingsDir);
        var commandReceivedByServer = false;

        await using var app = await CreateNodeApp(
            configureContainer: builder => builder
                .WithEnvironment("Audio__Recording__Enabled", "true")
                .WithBindMount(hostRecordingsDir, "/app/recordings"),
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", () =>
                {
                    commandReceivedByServer = true;
                    return Results.Ok();
                });
            });

        try
        {
            // Act
            await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));
            await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
            await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert — both streams were recorded and contain PCM data
            var rawFile = Assert.Single(Directory.GetFiles(hostRecordingsDir, "mic_raw_*.wav"));
            var processedFile = Assert.Single(Directory.GetFiles(hostRecordingsDir, "mic_processed_*.wav"));
            Assert.True(new FileInfo(rawFile).Length > 44);
            Assert.True(new FileInfo(processedFile).Length > 44);

            // Assert — no wake-word detection and no commands were sent
            var appLogs = await app.GetLogsAsync();
            Assert.DoesNotContain("[listener] Keyword detected", appLogs.Stdout);
            Assert.False(commandReceivedByServer);
        }
        finally
        {
            Directory.Delete(hostRecordingsDir, true);
        }
    }

    [Fact]
    public async Task RecordingDisabled_DoesNotRecord()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", () => Results.Ok());
            });

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.DoesNotContain("[recorder]", appLogs.Stdout);
    }
}
