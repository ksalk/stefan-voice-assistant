using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/nodes/register", async (HttpContext context, [FromServices] RegisterNode registerNode, [FromBody] RegisterNodeRequest request) =>
        {
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
            if(ipAddress == null)
            {
                return Results.BadRequest("Unable to determine IP address");
            }

            request.IpAddress = ipAddress;
            await registerNode.Handle(request, CancellationToken.None);
            return Results.Ok();
        });
    }
}