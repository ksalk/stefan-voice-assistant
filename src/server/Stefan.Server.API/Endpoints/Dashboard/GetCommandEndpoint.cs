using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandEndpoint
{
    public static void MapGetCommandEndpoint(this WebApplication app)
    {
        app.MapGet("api/commands/{commandId:guid}", async (
            Guid commandId,
            [FromServices] GetCommand getCommand,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommand.Handle(new GetCommandRequest { Id = commandId }, cancellationToken);

            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCommand")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);
    }
}
