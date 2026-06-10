using System.Net;

namespace Stefan.Node.IntegrationTests;

public class WakeWordDetectionTests : IntegrationTestBase
{
    [Fact]
    public async Task WakeWordDetected_WhenPassedValidWakeWordAudio()
    {
        // Arrange
        await using var app = await CreateNodeApp();

        // Act

        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));
        await app.WriteAudioFileAsync("TestAudioFiles/stefan01.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(0.5));
        await app.WriteAudioFileAsync("TestAudioFiles/how-much-longer.wav");
        await app.WriteSilenceAsync(TimeSpan.FromSeconds(3));

        // Assert
        //Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        //Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }
}