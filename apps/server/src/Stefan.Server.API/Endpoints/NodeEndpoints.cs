using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/nodes/register", async (HttpContext context, IConfiguration config, [FromServices] RegisterNode registerNode, [FromBody] RegisterNodeRequest request) =>
        {
            var nodeIpAddress = GetNodeIpAddress(context);
            if(nodeIpAddress == null)
            {
                return Results.BadRequest("Unable to determine node IP address");
            }

            request.IpAddress = nodeIpAddress;
            await registerNode.Handle(request, CancellationToken.None);
            return Results.Ok();
        })
        .RequireAuthorization(AuthPolicy.NodePolicy);

        app.MapPost("/api/nodes/{nodeId:guid}/ping", async (Guid nodeId, [FromServices] PingNode pingNode) =>
        {
            var result = await pingNode.Handle(new PingNodeRequest { NodeId = nodeId }, CancellationToken.None);

            if (result.ErrorMessage == "Node not found")
                return Results.NotFound(result.ErrorMessage);

            if (!result.Success)
                return Results.BadRequest(result.ErrorMessage);

            return Results.Ok(new
            {
                result.StatusReport!.CpuUsage,
                result.StatusReport.MemoryUsage,
                result.StatusReport.DiskUsage,
                result.StatusReport.Status
            });
        });
    }

    private static string? GetNodeIpAddress(HttpContext context)
    {
        var ip = context?.Connection?.RemoteIpAddress;
        if (ip != null && IPAddress.IsLoopback(ip))
        {
            ip = IPAddress.Loopback;
        }

        return ip?.ToString();
    }
}