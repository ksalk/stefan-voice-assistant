namespace Stefan.Server.Application.Services;

// TODO: can this be a record struct
public record struct LlmCommandResult(string ResponseText, IReadOnlyList<ConversationMessage> Messages, double DurationMs);

public record ConversationMessage(
    string Role,
    string? Content,
    IReadOnlyList<ToolCallRecord>? ToolCalls);

public record ToolCallRecord(
    string Id,
    string FunctionName,
    string Arguments,
    string? Result);
