using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class CommandProcessingTests : IntegrationTestBase
{
    private class NodeRegistrationPayload
    {
        public string NodeName { get; set; } = null!;
        public string SessionId { get; set; } = null!;
        public int Port { get; set; }
    }
    
    [Fact]
    public async Task CommandSentToServer_PlaysResponseAudio_WhenSuccessfulResponseReceived()
    {
        // Arrange
        var tempDirPath = Path.GetTempPath();
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
                
                    // some fixed audio bytes
                    var responseAudioBytes = new byte[] { 82, 73, 70, 70, 36, 0, 0, 0, 87, 65, 86, 0x00 };
                    return Results.File(responseAudioBytes, "audio/wav", "response.wav");
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
        var appLogs = await app.GetLogsAsync();
        Assert.Contains($"[audio] Playing audio: {tempDirPath}stefan_audio_", appLogs.Stdout);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(400)]
    public async Task CommandSentToServer_PlaysCommandFailedAudio_WhenFailedResponseReceived(int failureStatusCode)
    {
        // Arrange
        var tempDirPath = Path.GetTempPath();
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", async (HttpRequest request, HttpResponse response) => Results.StatusCode(failureStatusCode));
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
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[audio] Playing audio: /app/Assets/command_failed.wav", appLogs.Stdout);
    }

    [Fact]
    public async Task CommandSentToServer_GoesBackToListening_AfterResponse()
    {
        // Arrange
        var tempDirPath = Path.GetTempPath();
        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", () => Results.Ok());
                server.MapPost("/api/commands", async (HttpRequest request, HttpResponse response) => Results.Ok());
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
        var appLogs = await app.GetLogsAsync();
        Assert.Contains("[listener] Finished command processing. Returning to wake word detection.", appLogs.Stdout);
    }

    [Fact]
    public async Task CommandSentToServer_SendsCorrectDeviceIdAndSessionIdHeaders()
    {
        // Arrange
        string expectedNodeName = "stefan-node-test-name";
        string? registeredSessionId = null;
        string? commandDeviceId = null;
        string? commandSessionId = null;

        await using var app = await CreateNodeApp(
            configureContainer: builder => builder.WithEnvironment("Node__Name", expectedNodeName),
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", async (HttpRequest request) =>
                {
                    var payload = await request.ReadFromJsonAsync<NodeRegistrationPayload>();
                    registeredSessionId = payload?.SessionId;
                    return Results.Ok();
                });
                server.MapPost("/api/commands", (HttpRequest request) =>
                {
                    commandDeviceId = request.Headers["X-Node-Device-ID"].FirstOrDefault();
                    commandSessionId = request.Headers["X-Node-Session-ID"].FirstOrDefault();
                    return Results.Ok();
                });
            });

        // Act
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.5));
        await app.WriteAudioFileAsync("TestAudioFiles/how-much-longer.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));

        // Wait for the command request to land and headers to be captured
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (commandSessionId is null && !cts.IsCancellationRequested)
            await Task.Delay(250, cts.Token);

        // Assert
        Assert.NotNull(registeredSessionId);
        Assert.True(Guid.TryParse(registeredSessionId, out _));
        Assert.Equal(expectedNodeName, commandDeviceId);
        Assert.Equal(registeredSessionId, commandSessionId);
    }
}