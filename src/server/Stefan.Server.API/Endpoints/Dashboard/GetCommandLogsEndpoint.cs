using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Logs;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandLogsEndpoint
{
    public static void MapGetCommandLogsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/commands/{commandId:guid}/logs", async (
            Guid commandId,
            [FromServices] IHostEnvironment hostEnvironment,
            [FromServices] GetCommandLogs getCommandLogs,
            CancellationToken cancellationToken) =>
        {
            var request = new GetCommandLogsRequest
            {
                CommandId = commandId,
                Environment = hostEnvironment.EnvironmentName
            };
            var result = await getCommandLogs.Handle(request, cancellationToken);

            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCommandLogs");
    }
}
