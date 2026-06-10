using System.Net;

namespace Stefan.Node.IntegrationTests;

public class HealthEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await HttpClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk_AfterSendingSilence()
    {
        var silence = new byte[4096];
        await File.WriteAllBytesAsync(PipePath, silence);

        await Task.Delay(500);

        var response = await HttpClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
