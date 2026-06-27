using System.Net;

namespace Stefan.Server.IntegrationTests;

public class NodeRegistrationBindingTests : IntegrationTestBase
{
    private const string NodeSecret = "test-secret";

    [Fact]
    public async Task NodeRegistration_Returns400_WhenNodeNameIsMissing()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRawRegisterAsync(
            NodeSecret,
            """{ "sessionId": "s", "port": 1 }""",
            "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task NodeRegistration_Returns400_WhenSessionIdIsMissing()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRawRegisterAsync(
            NodeSecret,
            """{ "nodeName": "n", "port": 1 }""",
            "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task NodeRegistration_Returns400_WhenPortIsMissing()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRawRegisterAsync(
            NodeSecret,
            """{ "nodeName": "n", "sessionId": "s" }""",
            "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task NodeRegistration_Returns400_WhenRequestBodyIsMalformedJson()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRawRegisterAsync(
            NodeSecret,
            "this{is not json",
            "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task NodeRegistration_Returns415_WhenContentTypeIsTextPlain()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRawRegisterAsync(
            NodeSecret,
            """{ "nodeName": "n", "sessionId": "s", "port": 1 }""",
            "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
    }
}