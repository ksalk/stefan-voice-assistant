using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/nodes/register", async (HttpContext context, [FromServices] RegisterNode registerNode, [FromBody] RegisterNodeRequest request) =>
        {
            var ipAddress = GetIpAddress(context);
            if(ipAddress == null)
            {
                return Results.BadRequest("Unable to determine IP address");
            }

            request.IpAddress = ipAddress;
            await registerNode.Handle(request, CancellationToken.None);
            return Results.Ok();
        });
    }

    private static string? GetIpAddress(HttpContext context)
    {
        var ip = context?.Connection?.RemoteIpAddress;
        if (ip != null && IPAddress.IsLoopback(ip))
        {
            ip = IPAddress.Loopback;
        }

        return ip?.ToString();
    }
}