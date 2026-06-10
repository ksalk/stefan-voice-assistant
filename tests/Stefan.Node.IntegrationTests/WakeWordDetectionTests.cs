using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class WakeWordDetectionTests : IntegrationTestBase
{
    [Fact]
    public async Task WakeWordDetected_WhenPassedValidWakeWordAudio()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", () => Results.Ok());
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.5));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[listener] Keyword detected: stefan", appLogs.Stdout);
    }

    [Fact]
    public async Task WakeWordDetected_PlaysNotificationSound()
    {
        // Arrange
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", () => Results.Ok());
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.5));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[audio] Playing audio: /app/Assets/notification_sound.wav", appLogs.Stdout);
    }

    [Fact]
    public async Task WakeWordDetected_SendsCommandToServer_AndReceivesResponse()
    {
        // Arrange
        var commandReceivedByServer = false;
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", async (HttpRequest request, HttpResponse response) =>
                {
                    using var reader = new StreamReader(request.Body);
                    var audioData = await reader.ReadToEndAsync();
                    Console.WriteLine($"[mock server] Received audio data: {audioData.Length} bytes");
                    response.Headers["X-Response-Text"] = Uri.EscapeDataString("Test command received");
                    
                    commandReceivedByServer = true;
                    return Results.Ok();
                });
            }
        );

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.5));
        await app.WriteAudioFileAsync("TestAudioFiles/how-much-longer.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a moment for the audio to be processed

        // Assert
        Assert.True(commandReceivedByServer);

        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[http] Command sent successfully. Received response text: Test command received", appLogs.Stdout);
    }
}