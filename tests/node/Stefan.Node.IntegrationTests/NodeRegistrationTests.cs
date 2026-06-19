using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class NodeRegistrationTests : IntegrationTestBase
{
    private class NodeRegistrationRequest
    {
        public string NodeName { get; set; } = null!;
        public string SessionId { get; set; } = null!;
        public int Port { get; set; }
    }

    [Fact]
    public async Task NodeRegistersToServer_WithValidNodeName()
    {
        // Arrange
        string expectedNodeName = "stefan-node-test-name";
        string? receivedNodeName = null;

        await using var app = await CreateNodeApp(
            configureContainer: builder => builder.WithEnvironment("Node__Name", expectedNodeName),
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", async (HttpRequest request) =>
                {
                    var nodeInfo = await request.ReadFromJsonAsync<NodeRegistrationRequest>();
                    receivedNodeName = nodeInfo?.NodeName;
                    return Results.Ok();
                });
            });

        // Act — registration happens automatically on container start

        // Assert
        Assert.Equal(expectedNodeName, receivedNodeName);
    }

    [Fact]
    public async Task NodeRegistersToServer_SendsCorrectSecret()
    {
        // Arrange
        string expectedNodeSecret = "test-secret-value";
        string? receivedNodeSecret = null;

        await using var app = await CreateNodeApp(
            configureContainer: builder => builder.WithEnvironment("RemoteServer__AuthSecret", expectedNodeSecret),
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", async (HttpRequest request) =>
                {
                    receivedNodeSecret = request.Headers["X-Node-Secret"].FirstOrDefault();
                    return Results.Ok();
                });
            });

        // Act — registration happens automatically on container start

        // Assert
        Assert.Equal(expectedNodeSecret, receivedNodeSecret);
    }

    [Fact]
    public async Task NodeRegistersToServer_ExistsWhenRegistrationFails()
    {
        // Arrange
        string expectedNodeName = "stefan-node-test-name";

        await using var app = await CreateNodeApp(
            startMode: ContainerStartMode.ExpectExit,
            configureContainer: builder => builder.WithEnvironment("Node__Name", expectedNodeName),
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", async (HttpRequest request) =>
                {
                    return Results.Unauthorized();
                });
            });

        // Act
        var exitCode = await app.GetExitCodeAsync();
        var (stdout, stderr) = await app.GetLogsAsync();

        // Assert
        Assert.Equal(1L, exitCode);
        Assert.Contains("[fatal] Node registration failed. Server responded with 401 status code. Exiting.", stderr);
    }

    [Fact]
    public async Task NodeRegistersToServer_IncludesValidSessionIdAndConfiguredPort()
    {
        // Arrange
        string? capturedSessionId = null;
        int? capturedPort = null;

        await using var app = await CreateNodeApp(
            configureServer: server =>
            {
                server.MapPost("/api/nodes/register", async (HttpRequest request) =>
                {
                    var payload = await request.ReadFromJsonAsync<NodeRegistrationRequest>();
                    capturedSessionId = payload?.SessionId;
                    capturedPort = payload?.Port;
                    return Results.Ok();
                });
            });

        // Act — registration happens automatically on container start

        // Assert
        Assert.NotNull(capturedSessionId);
        Assert.True(Guid.TryParse(capturedSessionId, out _));
        Assert.Equal(8080, capturedPort);
    }
}
