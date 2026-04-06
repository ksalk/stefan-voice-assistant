using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;
using Stefan.Server.Common;
using Stefan.Server.Domain;

namespace Stefan.Server.API.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        app.MapGet("api/commands", async ([FromQuery] int page, [FromQuery] int pageSize, [FromServices] GetCommands getCommands) =>
        {
            var result = await getCommands.Handle(new GetCommandsRequest
            {
                Page = page,
                PageSize = pageSize,
            }, CancellationToken.None);

            return Results.Ok(result);
        })
        .WithName("GetCommands")
        .RequireCors("DashboardPolicy");

        app.MapPost("api/commands", async (HttpContext context, IFormFile file, [FromServices] ProcessCommand processCommand) =>
        {
            var deviceId = context.Request.Headers["X-Node-Device-ID"].FirstOrDefault();
            if (string.IsNullOrEmpty(deviceId))
            {
                ConsoleLog.Write(LogCategory.HTTP, "Command request rejected: missing X-Node-Device-ID header");
                return Results.BadRequest("Missing X-Node-Device-ID header");
            }

            var sessionId = context.Request.Headers["X-Node-Session-ID"].FirstOrDefault();
            if (string.IsNullOrEmpty(sessionId))
            {
                ConsoleLog.Write(LogCategory.HTTP, "Command request rejected: missing X-Node-Session-ID header");
                return Results.BadRequest("Missing X-Node-Session-ID header");
            }

            ConsoleLog.WriteSeparator();
            ConsoleLog.Write(LogCategory.HTTP, $"Received file: {file.FileName}, size: {file.Length} bytes");

            await using var fileStream = file.OpenReadStream();

            var result = await processCommand.Handle(new ProcessCommandRequest
            {
                DeviceId = deviceId,
                SessionId = sessionId,
                AudioStream = fileStream,
            }, CancellationToken.None);

            if (result == null)
            {
                return Results.Unauthorized();
            }

            context.Response.Headers["X-Response-Text"] = result.ResponseText;
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
        .RequireCors("DashboardPolicy");
    }
}
