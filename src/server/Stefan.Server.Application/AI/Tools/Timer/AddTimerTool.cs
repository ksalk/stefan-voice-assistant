using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class AddTimerTool(ToolsDbContext toolsDbContext, ITimerScheduler timerScheduler) : ITool
{
    public string Name => nameof(AddTimerTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
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

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasSeconds = argumentsJson.RootElement.TryGetProperty("seconds", out JsonElement seconds);
        bool hasLabel = argumentsJson.RootElement.TryGetProperty("label", out JsonElement label);

        if (!hasSeconds)
            throw new ArgumentNullException(nameof(seconds), "The seconds argument is required.");

        int secondsValue = seconds.GetInt32();
        string? labelValue = hasLabel ? label.GetString() : null;

        var entry = new TimerEntry
        {
            DurationInSeconds = secondsValue,
            Label = labelValue,
            CreatedAt = DateTime.UtcNow,
        };

        toolsDbContext.TimerEntries.Add(entry);
        await toolsDbContext.SaveChangesAsync(cancellationToken);

        await timerScheduler.ScheduleTimerAsync(entry, context.SourceDeviceId, cancellationToken);

        return string.IsNullOrEmpty(labelValue)
            ? $"Timer set for {secondsValue} seconds."
            : $"Timer '{labelValue}' set for {secondsValue} seconds.";
    }
}
