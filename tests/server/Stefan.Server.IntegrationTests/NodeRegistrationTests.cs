using System.Net;

namespace Stefan.Server.IntegrationTests;

public class NodeRegistrationTests : IntegrationTestBase
{
    private const string NodeSecret = "test-secret";

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

        var scheduledCount = await app.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM jobs.qrtz_job_details WHERE job_group = @group AND job_name = @name",
            new Dictionary<string, object?>
            {
                ["group"] = "NodePings",
                ["name"] = $"PingNode-{node.Id}",
            });
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
}