using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.Timer;

public class ListTimersTool(ToolsDbContext dbContext) : ITool
{
    public string Name => "list_timers";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "List active timers"
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context,  CancellationToken cancellationToken = default)
    {
        var timers = await dbContext.TimerEntries.ToListAsync(cancellationToken);
        if (timers.Count(t => !t.IsExpired) == 0)
            return "No active timers.";

        string response = "Active timers:\n";
        foreach (var timer in timers)
        {
            if (timer.IsExpired)
                continue;

            TimeSpan timeLeft = timer.ExpiresAt - DateTime.UtcNow;
            string labelPart = string.IsNullOrEmpty(timer.Label) ? "" : $" ({timer.Label})";
            response += $"- Timer {timer.Id}: {timeLeft:mm\\:ss} remaining{labelPart}\n";
        }

        return response;
    }
}
