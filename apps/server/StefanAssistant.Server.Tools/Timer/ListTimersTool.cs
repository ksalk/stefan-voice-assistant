using OpenAI.Chat;

namespace StefanAssistant.Server.Tools.Timer;

public static class ListTimersTool
{
    public static readonly ChatTool Definition = ChatTool.CreateFunctionTool(
        functionName: nameof(ListTimersTool),
        functionDescription: "List active timers"
    );

    public static string Execute(ChatToolCall toolCall, TimerDbContext dbContext)
    {
        var timers = dbContext.Timers.ToList();
        if (timers.Count == 0)
            return "No active timers.";

        string response = "Active timers:\n";
        foreach (var timer in timers)
        {
            TimeSpan timeLeft = TimeSpan.FromSeconds(timer.Seconds) - (DateTime.UtcNow - timer.CreatedAt);
            if (timeLeft.TotalSeconds > 0)
            {
                string labelPart = string.IsNullOrEmpty(timer.Label) ? "" : $" ({timer.Label})";
                response += $"- {timeLeft:mm\\:ss} remaining{labelPart}\n";
            }
        }

        return response;
    }
}
