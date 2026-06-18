using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class StopDetectionTests : IntegrationTestBase
{
    [Fact]
    public async Task StopKeywordDetected_WhenPassedValidStopKeywordAudio()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.8));
        await app.WriteAudioFileAsync("TestAudioFiles/stop.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[listener] Stop keyword detected. Returning to wake word detection.", appLogs.Stdout);
    }

    [Fact]
    public async Task StopKeywordDetected_DoesNotStopAudio_WhenNoAudioIsCurrentlyPlayed()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.8));
        await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for notification sound to finish playing
        await app.WriteAudioFileAsync("TestAudioFiles/stop.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[audio] No current audio playback to cancel.", appLogs.Stdout);
    }

    [Fact]
    public async Task StopKeywordDetected_StopsAudio_WhenAudioIsCurrentlyPlayed()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
            }
        );

        // Act
        var loremIpsumAudioFileBytes = await File.ReadAllBytesAsync("TestAudioFiles/lorem-ipsum.wav");
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(loremIpsumAudioFileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "lorem-ipsum.wav");
        await app.HttpClient.PostAsync("/audio", content);

        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.8));
        await app.WriteAudioFileAsync("TestAudioFiles/stop.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[audio] Cancelling current audio playback.", appLogs.Stdout);
    }

    [Fact]
    public async Task StopKeywordDetected_DoesNotSendCommandToServer()
    {
        // Arrange
        var commandReceivedByServer = false;
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", async (HttpRequest request, HttpResponse response) =>
                {
                    commandReceivedByServer = true;
                    return Results.Ok();
                });
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.8));
        await app.WriteAudioFileAsync("TestAudioFiles/stop.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        Assert.False(commandReceivedByServer, "No command should have been sent to the server.");
    }
}