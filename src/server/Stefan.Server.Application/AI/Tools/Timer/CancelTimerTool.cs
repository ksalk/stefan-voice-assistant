using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class CancelTimerTool(ToolsDbContext toolsDbContext, ITimerScheduler timerScheduler) : ITool
{
    public string Name => nameof(CancelTimerTool);
    
    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(CancelTimerTool),
        functionDescription: "Cancel a timer by its ID",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "timerId": {
                    "type": "integer",
                    "description": "The ID of the timer to cancel."
                }
            },
            "required": [ "timerId" ]
        }
        """u8.ToArray())
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasTimerId = argumentsJson.RootElement.TryGetProperty("timerId", out JsonElement timerId);

        if (!hasTimerId)
            throw new ArgumentNullException(nameof(timerId), "The timerId argument is required.");

        Guid timerIdValue = timerId.GetGuid();

        var timer = await toolsDbContext.TimerEntries.FindAsync(timerIdValue, cancellationToken);
        if (timer == null)
            return $"No timer found with ID {timerIdValue}.";

        toolsDbContext.TimerEntries.Remove(timer);
        await toolsDbContext.SaveChangesAsync(cancellationToken);

        await timerScheduler.CancelTimerAsync(timerIdValue, cancellationToken);

        return $"Timer with ID {timerIdValue} cancelled.";
    }
}
