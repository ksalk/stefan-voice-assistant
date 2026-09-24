using Stefan.Server.Application;
using Stefan.Server.Common;

namespace Stefan.Server.API.Endpoints;

public sealed record CommandHeaders(string DeviceId, string SessionId, Guid CommandId)
{
    public static Result<CommandHeaders> FromHttpRequest(HttpRequest request, ILogger logger)
    {
        var deviceId = request.Headers["X-Node-Device-ID"].FirstOrDefault();
        if (string.IsNullOrEmpty(deviceId))
        {
            logger.LogWarning("Command request rejected: missing X-Node-Device-ID header");
            return Failure("Missing X-Node-Device-ID header");
        }

        var sessionId = request.Headers["X-Node-Session-ID"].FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("Command request rejected: missing X-Node-Session-ID header (device {DeviceId})", deviceId);
            return Failure("Missing X-Node-Session-ID header");
        }

        var rawCommandId = request.Headers[Correlation.CommandIdHeader].FirstOrDefault();
        if (!Guid.TryParse(rawCommandId, out var commandId) || commandId == Guid.Empty)
        {
            logger.LogWarning(
                "Command request rejected: missing or invalid {Header} header '{Value}' (device {DeviceId})",
                Correlation.CommandIdHeader, rawCommandId, deviceId);
            return Failure($"Missing or invalid {Correlation.CommandIdHeader} header");
        }

        return Result<CommandHeaders>.Success(new CommandHeaders(deviceId, sessionId, commandId));
    }

    private static Result<CommandHeaders> Failure(string message) =>
        Result<CommandHeaders>.Failure(Error.Validation(message));
}
