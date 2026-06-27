using System.Net;

namespace Stefan.Server.IntegrationTests;

public class AuthTests : IntegrationTestBase
{
    private static readonly RegisterNodeRequestDto ValidBody = new("node-auth", "s-1", 8080);

    [Fact]
    public async Task NodeRegistration_Returns401_WhenSecretIsMissing()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRegisterNodeAsync(nodeSecret: null, ValidBody);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var nodes = await app.Client.GetNodesAsync();
        Assert.Empty(nodes.Nodes);
    }

    [Fact]
    public async Task NodeRegistration_Returns401_WhenSecretIsEmpty()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRegisterNodeAsync(nodeSecret: "", ValidBody);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var nodes = await app.Client.GetNodesAsync();
        Assert.Empty(nodes.Nodes);
    }

    [Fact]
    public async Task NodeRegistration_Returns401_WhenSecretIsWrong()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRegisterNodeAsync(nodeSecret: "wrong-secret", ValidBody);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var nodes = await app.Client.GetNodesAsync();
        Assert.Empty(nodes.Nodes);
    }

    [Fact]
    public async Task NodeRegistration_Returns200_WhenSecretIsCorrect()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRegisterNodeAsync(nodeSecret: "test-secret", ValidBody);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var nodes = await app.Client.GetNodesAsync();
        Assert.NotEmpty(nodes.Nodes);
    }

    [Fact]
    public async Task NodeRegistration_Returns401_WhenServerSecretIsUnset()
    {
        await using var app = await CreateServerApp(
            configureContainer: b => b.WithEnvironment("NodeSecret", ""));

        var res = await app.Client.PostRegisterNodeAsync(nodeSecret: "test-secret", ValidBody);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var nodes = await app.Client.GetNodesAsync();
        Assert.Empty(nodes.Nodes);
    }
}