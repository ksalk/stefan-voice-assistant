using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandToolsEndpoint
{
    public static void MapGetCommandToolsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/commands/{commandId:guid}/tools", async (
            Guid commandId,
            [FromServices] GetCommandTools getCommandTools,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommandTools.Handle(new GetCommandToolsRequest { CommandId = commandId }, cancellationToken);

            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCommandTools");
    }
}
