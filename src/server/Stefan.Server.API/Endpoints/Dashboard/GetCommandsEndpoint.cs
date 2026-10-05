using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;
using Stefan.Server.Domain;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandsEndpoint
{
    public static void MapGetCommandsEndpoint(this WebApplication app)
    {
        app.MapGet("api/commands", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] Guid? nodeId,
            [FromQuery] CommandStatus[] status,
            [FromServices] GetCommands getCommands,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommands.Handle(new GetCommandsRequest
            {
                Page = page,
                PageSize = pageSize,
                NodeId = nodeId,
                Statuses = status.ToList(),
            }, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetCommands")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);
    }
}
