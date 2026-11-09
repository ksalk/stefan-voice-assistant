using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class ClearNodeStatusReportsEndpoint
{
    public static void MapClearNodeStatusReportsEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/api/nodes/{nodeId:guid}/status-reports", async (
            Guid nodeId,
            [FromQuery] int? olderThanDays,
            [FromServices] ClearNodeStatusReports clearNodeStatusReports,
            CancellationToken cancellationToken) =>
        {
            var result = await clearNodeStatusReports.Handle(
                new ClearNodeStatusReportsRequest
                {
                    NodeId = nodeId,
                    OlderThanDays = olderThanDays ?? 30
                },
                cancellationToken);

            return result.ToHttpResult(value => Results.Ok(value));
        });
    }
}
