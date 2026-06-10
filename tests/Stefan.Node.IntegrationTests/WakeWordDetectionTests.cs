using System.Net;
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
                server.MapPost("/api/commands", async (HttpRequest request, HttpResponse response) =>
                {
                    using var reader = new StreamReader(request.Body);
                    var audioData = await reader.ReadToEndAsync();
                    Console.WriteLine($"[mock server] Received audio data: {audioData.Length} bytes");
                    response.Headers["X-Response-Text"] = Uri.EscapeDataString("Test command received");
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
        //Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        //Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }
}