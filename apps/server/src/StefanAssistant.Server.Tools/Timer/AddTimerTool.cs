using System.Text.Json;
using OpenAI.Chat;

namespace StefanAssistant.Server.Tools.Timer;

public static class AddTimerTool
{
    public static readonly ChatTool Definition = ChatTool.CreateFunctionTool(
        functionName: nameof(AddTimerTool),
        functionDescription: "Add a timer",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "seconds": {
                    "type": "integer",
                    "description": "Number of seconds for the timer."
                },
                "label": {
                    "type": "string",
                    "description": "A label for the timer if desired."
                }
            },
            "required": [ "seconds" ]
        }
        """u8.ToArray())
    );

    public static string Execute(ChatToolCall toolCall, TimerDbContext dbContext)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasSeconds = argumentsJson.RootElement.TryGetProperty("seconds", out JsonElement seconds);
        bool hasLabel = argumentsJson.RootElement.TryGetProperty("label", out JsonElement label);

        if (!hasSeconds)
            throw new ArgumentNullException(nameof(seconds), "The seconds argument is required.");

        int secondsValue = seconds.GetInt32();
        string? labelValue = hasLabel ? label.GetString() : null;

        TimerEntry entry = new()
        {
            Seconds = secondsValue,
            Label = labelValue,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Timers.Add(entry);
        dbContext.SaveChanges();

        return string.IsNullOrEmpty(labelValue)
            ? $"Timer set for {secondsValue} seconds."
            : $"Timer '{labelValue}' set for {secondsValue} seconds.";
    }
}
