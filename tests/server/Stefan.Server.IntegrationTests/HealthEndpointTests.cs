namespace Stefan.Server.IntegrationTests;

public class HealthEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Arrange
        await using var app = await CreateServerApp();

        // Act
        var health = await app.Client.GetHealthAsync();

        // Assert
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("XAi", health.SttProvider);
        Assert.Equal("XAi", health.TtsProvider);
    }
}