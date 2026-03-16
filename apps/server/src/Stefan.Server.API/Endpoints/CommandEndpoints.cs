using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.AI;
using Stefan.Server.Common;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.API.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        app.MapPost("api/commands", async (HttpContext context, IFormFile file, SpeechToTextService stt, LlmCommandService llm, IConfiguration config, StefanDbContext dbContext) =>
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

            var expectedSecret = config["NodeSecret"];
            var providedSecret = context.Request.Headers["X-Node-Secret"].FirstOrDefault();

            if (string.IsNullOrEmpty(expectedSecret) || providedSecret != expectedSecret)
            {
                ConsoleLog.Write(LogCategory.HTTP, "Command request rejected: invalid or missing X-Node-Secret");
                return Results.Unauthorized();
            }

            // Check if device is registered in database
            // TODO: optimize by caching registered nodes in memory to avoid DB hit on every command
            var node = await dbContext.Nodes.FirstOrDefaultAsync(n => n.Name == deviceId);
            if (node == null)
            {
                ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: device '{deviceId}' not registered");
                return Results.Unauthorized();
            }

            // Verify session ID matches
            if (node.CurrentSessionId != sessionId)
            {
                ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: invalid session ID for device '{deviceId}'");
                return Results.Unauthorized();
            }

            var timestamp = Stopwatch.GetTimestamp();
            ConsoleLog.WriteSeparator();
            ConsoleLog.Write(LogCategory.HTTP, $"Received file: {file.FileName}, size: {file.Length} bytes");

            using var fileStream = file.OpenReadStream();

            string transcript = await stt.TranscribeAsync(fileStream);
            ConsoleLog.Write(LogCategory.STT, $"Transcription result: {transcript}");
            ConsoleLog.Write(LogCategory.STT, $"Speech processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

            timestamp = Stopwatch.GetTimestamp();
            string response = llm.ProcessCommand(transcript, deviceId);

            ConsoleLog.Write(LogCategory.LLM, $"LLM processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

            return Results.Ok(response);
        })
        .DisableAntiforgery() // TODO: fix in future for security
        .WithName("ProcessCommand");
    }
}