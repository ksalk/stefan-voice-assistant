using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;
using Stefan.Server.Common;

namespace Stefan.Server.API.Endpoints.Node;

public static class ProcessCommandEndpoint
{
    public static void MapProcessCommandEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("api/commands", async (
            HttpContext context,
            IFormFile file,
            IFormFile? wakeWordFile,
            [FromServices] ProcessCommand processCommand,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Stefan.Server.API.Endpoints.Node.ProcessCommandEndpoint");
            var headersResult = CommandHeaders.FromHttpRequest(context.Request, logger);
            if (!headersResult.IsSuccess)
            {
                return headersResult.Error!.ToHttpResult();
            }

            var headers = headersResult.Value!;

            logger.LogInformation(
                "Received command {CommandId} from device {DeviceId}: {FileName}, {FileSize} bytes; wake word audio: {WakeWordFileName}, {WakeWordFileSize} bytes",
                headers.CommandId, headers.DeviceId, file.FileName, file.Length,
                wakeWordFile?.FileName ?? "(none)", wakeWordFile?.Length ?? 0);

            await using var fileStream = file.OpenReadStream();
            await using var wakeWordStream = wakeWordFile?.OpenReadStream();

            var result = await processCommand.Handle(new ProcessCommandRequest
            {
                CommandId = headers.CommandId,
                DeviceId = headers.DeviceId,
                SessionId = headers.SessionId,
                AudioStream = fileStream,
                WakeWordAudioStream = wakeWordStream,
            }, cancellationToken);

            return result.ToHttpResult(response =>
            {
                context.Response.Headers[Correlation.CommandIdHeader] = headers.CommandId.ToString();
                context.Response.Headers["X-Response-Text"] = Uri.EscapeDataString(response.ResponseText);
                return Results.File(response.AudioBytes, "audio/wav", "response.wav");
            });
        })
        .DisableAntiforgery()
        .WithName("ProcessCommand");
    }
}
