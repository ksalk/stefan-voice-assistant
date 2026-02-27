namespace StefanAssistant.Server.Tools.Timer;

public static class TimerTools
{
    public static string AddTimer(int seconds, string? label, TimerDbContext dbContext)
    {
        TimerEntry entry = new()
        {
            Seconds = seconds,
            Label = label,
            CreatedAt = DateTime.UtcNow,
        };

        if (string.IsNullOrEmpty(label))
        {
            Console.WriteLine($"Adding timer for {seconds} seconds");
        
            dbContext.Timers.Add(entry);
            dbContext.SaveChanges();
            return $"Timer set for {seconds} seconds.";
        }
        
        Console.WriteLine($"Adding timer: {label} for {seconds} seconds");

        dbContext.Timers.Add(entry);
        dbContext.SaveChanges();
        
        return $"Timer '{label}' set for {seconds} seconds.";
    }

    public static string ListTimers(TimerDbContext dbContext)
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

    public static string CancelTimer(int timerId, TimerDbContext dbContext)
    {
        var timer = dbContext.Timers.Find(timerId);
        if (timer == null)
            return $"No timer found with ID {timerId}.";

        dbContext.Timers.Remove(timer);
        dbContext.SaveChanges();
        return $"Timer with ID {timerId} cancelled.";
    }
}