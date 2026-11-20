namespace Stefan.Node.Services;

public enum CommandDispatchStatus
{
    Success,
    NoSpeech,
    Failed
}

public sealed record CommandDispatchResult(
    CommandDispatchStatus Status,
    CommandResponse? Response = null,
    string? Error = null)
{
    public static CommandDispatchResult Success(CommandResponse response) =>
        new(CommandDispatchStatus.Success, response);

    public static CommandDispatchResult NoSpeech(string? detail = null) =>
        new(CommandDispatchStatus.NoSpeech, Error: detail);

    public static CommandDispatchResult Failed(string error) =>
        new(CommandDispatchStatus.Failed, Error: error);
}
