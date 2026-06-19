using System.Net;

namespace Stefan.Node.IntegrationTests;

public class HealthEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Arrange
        await using var app = await CreateNodeApp();

        // Act
        var response = await app.Client.GetHealthAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk_AfterSendingSilence()
    {
        // Arrange
        await using var app = await CreateNodeApp();
        await app.WriteSilenceAsync(TimeSpan.FromMilliseconds(500));
        await Task.Delay(500);

        // Act
        var response = await app.Client.GetHealthAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
