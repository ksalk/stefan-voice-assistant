using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetCommandAudioEndpoint
{
    public static void MapGetCommandAudioEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/commands/{commandId:guid}/audio", async (
            Guid commandId,
            [FromQuery] AudioType type,
            [FromServices] GetCommandAudio getCommandAudio,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommandAudio.Handle(new GetCommandAudioRequest
            {
                CommandId = commandId,
                Type = type,
            }, cancellationToken);

            return result.ToHttpResult(r => Results.File(r.AudioBytes, r.ContentType, r.FileName));
        })
        .WithName("GetCommandAudio");
    }
}
