using System.Net;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class NodeRegistrationTests : IntegrationTestBase
{
    private const string ExpectedNodeName = "stefan-node-test-name";
    private string? _receivedNodeName;

    private class NodeRegistrationRequest
    {
        public string NodeName { get; set; } = null!;
    }

    protected override void ConfigureMockServer(WebApplication app)
    {
        app.MapPost("/api/nodes/register", async (HttpRequest request) =>
        {
            var nodeInfo = await request.ReadFromJsonAsync<NodeRegistrationRequest>();
            _receivedNodeName = nodeInfo?.NodeName;
            return Results.Ok();
        });
    }

    protected override ContainerBuilder ConfigureContainer(ContainerBuilder builder)
    {
        return builder.WithEnvironment("Node__Name", ExpectedNodeName);
    }
    
    [Fact]
    public async Task NodeRegistersToServer_WithValidNodeName()
    {
        Assert.Equal(ExpectedNodeName, _receivedNodeName);
    }
}
