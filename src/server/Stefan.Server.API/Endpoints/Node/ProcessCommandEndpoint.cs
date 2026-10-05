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
                "Received command {CommandId} from device {DeviceId}: {FileName}, {FileSize} bytes",
                headers.CommandId, headers.DeviceId, file.FileName, file.Length);

            await using var fileStream = file.OpenReadStream();

            var result = await processCommand.Handle(new ProcessCommandRequest
            {
                CommandId = headers.CommandId,
                DeviceId = headers.DeviceId,
                SessionId = headers.SessionId,
                AudioStream = fileStream,
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
