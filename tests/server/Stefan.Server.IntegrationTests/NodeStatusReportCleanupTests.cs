using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Stefan.Server.IntegrationTests;

public class NodeStatusReportCleanupTests : IntegrationTestBase
{
    private const string NodeSecret = "test-secret";

    [Fact]
    public async Task ClearNodeStatusReports_DeletesOnlyReportsOlderThanWindow()
    {
        await using var app = await CreateServerApp();

        var nodeId = await RegisterNodeAsync(app);
        var oldTimestamp = DateTime.UtcNow.AddDays(-31);
        var recentTimestamp = DateTime.UtcNow.AddDays(-1);

        await InsertStatusReportAsync(app, nodeId, oldTimestamp);
        await InsertStatusReportAsync(app, nodeId, recentTimestamp);

        var response = await app.Client.ClearNodeStatusReportsAsync(nodeId, olderThanDays: 30);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ClearNodeStatusReportsResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.DeletedCount);

        var details = await app.Client.GetNodeDetailsAsync(nodeId);
        var remaining = Assert.Single(details.Node.StatusReports);
        Assert.Equal(recentTimestamp, remaining.Timestamp, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ClearNodeStatusReports_Returns404_WhenNodeDoesNotExist()
    {
        await using var app = await CreateServerApp();

        var response = await app.Client.ClearNodeStatusReportsAsync(Guid.NewGuid(), olderThanDays: 30);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClearNodeStatusReports_Returns400_WhenOlderThanDaysIsInvalid()
    {
        await using var app = await CreateServerApp();

        var response = await app.Client.ClearNodeStatusReportsAsync(Guid.NewGuid(), olderThanDays: 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> RegisterNodeAsync(ServerApp app)
    {
        var response = await app.Client.PostRegisterNodeAsync(
            NodeSecret,
            new RegisterNodeRequestDto("cleanup-node", "s-1", 8080));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var nodes = await app.Client.GetNodesAsync();
        return Assert.Single(nodes.Nodes).Id;
    }

    private static async Task InsertStatusReportAsync(ServerApp app, Guid nodeId, DateTime timestamp)
    {
        await using var connection = new NpgsqlConnection(app.DbConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "NodeStatusReports" ("Id", "NodeId", "Timestamp", "Status")
            VALUES (@id, @nodeId, @timestamp, 0)
            """,
            connection);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("nodeId", nodeId);
        command.Parameters.AddWithValue("timestamp", DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync();
    }
}
