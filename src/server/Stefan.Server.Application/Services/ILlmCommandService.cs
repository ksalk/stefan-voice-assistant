namespace Stefan.Server.Application.Services;

public interface ILlmCommandService
{
    Task<Result<LlmCommandResult>> ProcessCommandAsync(string command, string deviceId, Guid commandId, CancellationToken cancellationToken = default);
}
