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
            [FromServices] GetCommands getCommands) =>
        {
            var result = await getCommands.Handle(new GetCommandsRequest
            {
                Page = page,
                PageSize = pageSize,
                NodeId = nodeId,
                Statuses = status.ToList(),
            }, CancellationToken.None);

            return Results.Ok(result);
        })
        .WithName("GetCommands")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

        app.MapGet("api/commands/{commandId:guid}", async (Guid commandId, [FromServices] GetCommand getCommand) =>
        {
            var result = await getCommand.Handle(new GetCommandRequest { Id = commandId }, CancellationToken.None);

            if (result == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(result);
        })
        .WithName("GetCommand")
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

        app.MapPost("api/commands", async (HttpContext context, IFormFile file, [FromServices] ProcessCommand processCommand) =>
        {
            var deviceId = context.Request.Headers["X-Node-Device-ID"].FirstOrDefault();
            if (string.IsNullOrEmpty(deviceId))
            {
                logger.LogWarning("Command request rejected: missing X-Node-Device-ID header");
                return Results.BadRequest("Missing X-Node-Device-ID header");
            }

            var sessionId = context.Request.Headers["X-Node-Session-ID"].FirstOrDefault();
            if (string.IsNullOrEmpty(sessionId))
            {
                logger.LogWarning("Command request rejected: missing X-Node-Session-ID header (device {DeviceId})", deviceId);
                return Results.BadRequest("Missing X-Node-Session-ID header");
            }

            var rawCommandId = context.Request.Headers[Correlation.CommandIdHeader].FirstOrDefault();
            if (!Guid.TryParse(rawCommandId, out var commandId) || commandId == Guid.Empty)
            {
                logger.LogWarning(
                    "Command request rejected: missing or invalid {Header} header '{Value}' (device {DeviceId})",
                    Correlation.CommandIdHeader, rawCommandId, deviceId);
                return Results.BadRequest($"Missing or invalid {Correlation.CommandIdHeader} header");
            }

            logger.LogInformation(
                "Received command {CommandId} from device {DeviceId}: {FileName}, {FileSize} bytes",
                commandId, deviceId, file.FileName, file.Length);

            await using var fileStream = file.OpenReadStream();

            var result = await processCommand.Handle(new ProcessCommandRequest
            {
                CommandId = commandId,
                DeviceId = deviceId,
                SessionId = sessionId,
                AudioStream = fileStream,
            }, CancellationToken.None);

            if (result == null)
            {
                return Results.Unauthorized();
            }

            context.Response.Headers[Correlation.CommandIdHeader] = commandId.ToString();
            context.Response.Headers["X-Response-Text"] = Uri.EscapeDataString(result.ResponseText);
            return Results.File(result.AudioBytes, "audio/wav", "response.wav");
        })
        .RequireAuthorization(AuthPolicy.NodePolicy)
        .DisableAntiforgery()
        .WithName("ProcessCommand");

        app.MapGet("api/commands/{commandId:guid}/audio", async (Guid commandId, [FromQuery] AudioType type, [FromServices] GetCommandAudio getCommandAudio) =>
        {
            var result = await getCommandAudio.Handle(new GetCommandAudioRequest
            {
                CommandId = commandId,
                Type = type,
            }, CancellationToken.None);

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
