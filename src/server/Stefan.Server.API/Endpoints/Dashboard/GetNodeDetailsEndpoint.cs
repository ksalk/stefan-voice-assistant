using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetNodeDetailsEndpoint
{
    public static void MapGetNodeDetailsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/api/nodes/{nodeId:guid}", async (
            Guid nodeId,
            [FromServices] GetNodeDetails getNodeDetails,
            CancellationToken cancellationToken) =>
        {
            var result = await getNodeDetails.Handle(new GetNodeDetailsRequest { NodeId = nodeId }, cancellationToken);
            return result.ToHttpResult(Results.Ok);
        });
    }
}
