using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class NodeRegistrationTests : IntegrationTestBase
{
    private class NodeRegistrationRequest
    {
        public string NodeName { get; set; } = null!;
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
}
