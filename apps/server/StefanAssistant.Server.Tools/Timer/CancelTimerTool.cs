using System.Text.Json;
using OpenAI.Chat;

namespace StefanAssistant.Server.Tools.Timer;

public static class CancelTimerTool
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

    public static string Execute(ChatToolCall toolCall, TimerDbContext dbContext)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasTimerId = argumentsJson.RootElement.TryGetProperty("timerId", out JsonElement timerId);

        if (!hasTimerId)
            throw new ArgumentNullException(nameof(timerId), "The timerId argument is required.");

        var timer = dbContext.Timers.Find(timerId.GetInt32());
        if (timer == null)
            return $"No timer found with ID {timerId.GetInt32()}.";

        dbContext.Timers.Remove(timer);
        dbContext.SaveChanges();
        return $"Timer with ID {timerId.GetInt32()} cancelled.";
    }
}
