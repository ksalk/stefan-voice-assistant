using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Logs;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandLogsEndpoint
{
    public static void MapGetCommandLogsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/commands/{commandId:guid}/logs", async (
            Guid commandId,
            [FromServices] GetCommandLogs getCommandLogs,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommandLogs.Handle(new GetCommandLogsRequest { CommandId = commandId }, cancellationToken);

            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCommandLogs");
    }
}
