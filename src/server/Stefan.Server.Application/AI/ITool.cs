using OpenAI.Chat;

namespace Stefan.Server.Application.AI;

public interface ITool
{
    public static ChatTool Definition => throw new NotImplementedException();

    Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default);
}

public class ToolCallContext
{
    public required string SourceDeviceId { get; set; }

    // Additional context properties can be added here as needed
}