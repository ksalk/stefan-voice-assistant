namespace Stefan.Server.Application.Services;

public interface ILlmCommandService
{
    Task<Result<LlmCommandResult>> ProcessCommandAsync(string command, string deviceId, CancellationToken cancellationToken = default);
}
