using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;
using Stefan.Server.Common;
using Stefan.Server.Domain;

namespace Stefan.Server.API.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Stefan.Server.API.Endpoints.CommandEndpoints");

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

        app.MapGet("api/commands/{commandId:guid}", async (
            Guid commandId,
            [FromServices] GetCommand getCommand,
            CancellationToken cancellationToken) =>
        {
            var result = await getCommand.Handle(new GetCommandRequest { Id = commandId }, cancellationToken);

            if (result == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(result);
        })
        .WithName("GetCommand")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

        app.MapPost("api/commands", async (
            HttpContext context,
            IFormFile file,
            [FromServices] ProcessCommand processCommand,
            CancellationToken cancellationToken) =>
        {
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
        .RequireAuthorization(AuthPolicy.NodePolicy)
        .DisableAntiforgery()
        .WithName("ProcessCommand");

        app.MapGet("api/commands/{commandId:guid}/audio", async (
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

            if (result == null)
            {
                return Results.NotFound();
            }

            return Results.File(result.AudioBytes, result.ContentType, result.FileName);
        })
        .WithName("GetCommandAudio")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);
    }
}
