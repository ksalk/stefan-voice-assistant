using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class ListTimersTool(TimerDbContext dbContext) : ITool
{
    public string Name => nameof(ListTimersTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(ListTimersTool),
        functionDescription: "List active timers"
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context,  CancellationToken cancellationToken = default)
    {
        var timers = await dbContext.Timers.ToListAsync(cancellationToken);
        if (timers.Count == 0)
            return "No active timers.";

        string response = "Active timers:\n";
        foreach (var timer in timers)
        {
            TimeSpan timeLeft = TimeSpan.FromSeconds(timer.Seconds) - (DateTime.UtcNow - timer.CreatedAt);
            if (timeLeft.TotalSeconds > 0)
            {
                string labelPart = string.IsNullOrEmpty(timer.Label) ? "" : $" ({timer.Label})";
                response += $"- Timer {timer.Id}: {timeLeft:mm\\:ss} remaining{labelPart}\n";
            }
        }

        return response;
    }
}
