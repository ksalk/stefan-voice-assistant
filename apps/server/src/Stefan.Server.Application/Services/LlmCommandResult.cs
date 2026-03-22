namespace Stefan.Server.Application.Services;

public record LlmCommandResult(string ResponseText, IReadOnlyList<ConversationMessage> Messages);

public record ConversationMessage(
    string Role,
    string? Content,
    IReadOnlyList<ToolCallRecord>? ToolCalls);

public record ToolCallRecord(
    string Id,
    string FunctionName,
    string Arguments,
    string? Result);
