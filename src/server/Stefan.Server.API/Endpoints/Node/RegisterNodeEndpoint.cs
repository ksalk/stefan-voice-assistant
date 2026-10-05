using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Node;

public sealed class RegisterNodeBody
{
    public required string NodeName { get; set; }
    public required string SessionId { get; set; }
    public required int Port { get; set; }
}

public static class RegisterNodeEndpoint
{
    public static void MapRegisterNodeEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/api/nodes/register", async (
            HttpContext context,
            [FromBody] RegisterNodeBody body,
            [FromServices] RegisterNode registerNode,
            CancellationToken cancellationToken) =>
        {
            var nodeIpAddress = GetNodeIpAddress(context);
            if(nodeIpAddress == null)
            {
                return Results.BadRequest("Unable to determine node IP address");
            }

            await registerNode.Handle(new RegisterNodeRequest
            {
                NodeName = body.NodeName,
                SessionId = body.SessionId,
                Port = body.Port,
                IpAddress = nodeIpAddress,
            }, cancellationToken);

            return Results.Ok();
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
