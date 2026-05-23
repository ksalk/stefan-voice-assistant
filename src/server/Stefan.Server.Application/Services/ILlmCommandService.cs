namespace Stefan.Server.Application.Services;

public interface ILlmCommandService
{
    Task<LlmCommandResult> ProcessCommandAsync(string command, string deviceId, CancellationToken cancellationToken = default);
}
