using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class PingNodeEndpoint
{
    public static void MapPingNodeEndpoint(this WebApplication app)
    {
        app.MapPost("/api/nodes/{nodeId:guid}/ping", async (
            Guid nodeId,
            [FromServices] PingNode pingNode,
            CancellationToken cancellationToken) =>
        {
            var result = await pingNode.Handle(new PingNodeRequest { NodeId = nodeId }, cancellationToken);

            return result.ToHttpResult(statusReport => statusReport == null
                ? Results.Ok()
                : Results.Ok(new
                {
                    statusReport.CpuUsage,
                    statusReport.MemoryUsage,
                    statusReport.DiskUsage,
                    statusReport.AudioVolume,
                    statusReport.Status
                }));
        })
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);
    }
}
