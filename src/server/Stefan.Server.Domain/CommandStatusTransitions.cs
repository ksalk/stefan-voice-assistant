namespace Stefan.Server.Domain;

internal static class CommandStatusTransitions
{
    private static readonly Dictionary<CommandStatus, CommandStatus[]> Allowed = new()
    {
        [CommandStatus.Received] =
        [
            CommandStatus.SttSuccess,
            CommandStatus.SttFailed,
            CommandStatus.Failed,
            CommandStatus.Canceled
        ],
        [CommandStatus.SttSuccess] =
        [
            CommandStatus.LlmSuccess,
            CommandStatus.LlmFailed,
            CommandStatus.Failed,
            CommandStatus.Canceled
        ],
        [CommandStatus.LlmSuccess] =
        [
            CommandStatus.TtsSuccess,
            CommandStatus.TtsFailed,
            CommandStatus.Failed,
            CommandStatus.Canceled
        ],
        [CommandStatus.TtsSuccess] =
        [
            CommandStatus.Completed,
            CommandStatus.Failed,
            CommandStatus.Canceled
        ],
    };

    public static bool IsTerminal(CommandStatus status) =>
        status is CommandStatus.Completed
            or CommandStatus.SttFailed
            or CommandStatus.LlmFailed
            or CommandStatus.TtsFailed
            or CommandStatus.HttpFailed
            or CommandStatus.Failed
            or CommandStatus.Canceled;

    public static void Ensure(CommandStatus from, CommandStatus to)
    {
        if (!Allowed.TryGetValue(from, out var allowed) || !allowed.Contains(to))
        {
            throw new CommandTransitionException(from, to);
        }
    }
}

public sealed class CommandTransitionException(CommandStatus from, CommandStatus to)
    : InvalidOperationException($"Command cannot transition from {from} to {to}.")
{
    public CommandStatus From { get; } = from;
    public CommandStatus To { get; } = to;
}
