using System.Text.Json;
using OpenAI.Chat;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class CancelTimerTool(TimerDbContext dbContext, ITimerScheduler timerScheduler)
{
    public static readonly ChatTool Definition = ChatTool.CreateFunctionTool(
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

    public async Task<string> ExecuteAsync(ChatToolCall toolCall, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasTimerId = argumentsJson.RootElement.TryGetProperty("timerId", out JsonElement timerId);

        if (!hasTimerId)
            throw new ArgumentNullException(nameof(timerId), "The timerId argument is required.");

        int timerIdValue = timerId.GetInt32();

        var timer = await dbContext.Timers.FindAsync(timerIdValue, cancellationToken);
        if (timer == null)
            return $"No timer found with ID {timerIdValue}.";

        dbContext.Timers.Remove(timer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await timerScheduler.CancelTimerAsync(timerIdValue, cancellationToken);

        return $"Timer with ID {timerIdValue} cancelled.";
    }
}
