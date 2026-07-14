using System.Net;

namespace Stefan.Server.IntegrationTests;

public class NodeRegistrationTests : IntegrationTestBase
{
    private const string NodeSecret = "test-secret";
    private static readonly RegisterNodeRequestDto ValidBody = new("node-auth", "s-1", 8080);

    [Fact]
    public async Task NodeRegistration_PersistsNodeAndSchedulesPing_WhenRegistrationSucceeds()
    {
        await using var app = await CreateServerApp();

        var res = await app.Client.PostRegisterNodeAsync(
            NodeSecret, new RegisterNodeRequestDto("node-1", "s-1", 8080));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var nodes = await app.Client.GetNodesAsync();
        var node = Assert.Single(nodes.Nodes);

        Assert.Equal("node-1", node.Name);
        Assert.Equal("s-1", node.CurrentSessionId);
        Assert.Equal(8080, node.Port);
        Assert.Equal("Online", node.Status);
        Assert.Equal(0, node.RestartCount);
        Assert.NotEmpty(node.LastKnownIpAddress);
        Assert.NotEqual(default, node.RegisteredAt);
        Assert.NotNull(node.LastSeenAt);
        Assert.Null(node.LastPingAt);

        var scheduledCount = await app.JobStore.CountJobs("NodePings", $"PingNode-{node.Id}");
        Assert.Equal(1, scheduledCount);
    }

    [Fact]
    public async Task GetNodeDetails_ReturnsFullNodeWithEmptyStatusReports_WhenNodeIsRegistered()
    {
        await using var app = await CreateServerApp();

        await app.Client.PostRegisterNodeAsync(
            NodeSecret, new RegisterNodeRequestDto("node-2", "s-2", 9000));

        var nodes = await app.Client.GetNodesAsync();
        var nodeSummary = Assert.Single(nodes.Nodes);
        Assert.Equal("node-2", nodeSummary.Name);

        var details = await app.Client.GetNodeDetailsAsync(nodeSummary.Id);

        Assert.Equal("node-2", details.Node.Name);
        Assert.Equal("s-2", details.Node.CurrentSessionId);
        Assert.Equal(9000, details.Node.Port);
        Assert.Equal("Online", details.Node.Status);
        Assert.Equal(0, details.Node.RestartCount);
        Assert.Empty(details.Node.StatusReports);
    }

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