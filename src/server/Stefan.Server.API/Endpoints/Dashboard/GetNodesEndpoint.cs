using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetNodesEndpoint
{
    public static void MapGetNodesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/api/nodes", async (
            [FromServices] GetNodes getNodes,
            CancellationToken cancellationToken) =>
        {
            var result = await getNodes.Handle(new GetNodesRequest(), cancellationToken);
            return Results.Ok(result);
        });
    }
}
