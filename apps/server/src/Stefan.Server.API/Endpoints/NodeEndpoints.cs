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