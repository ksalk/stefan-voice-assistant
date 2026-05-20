namespace Stefan.Server.Domain.ToolEntities;

public class TimerEntry
{
    public Guid Id { get; set; }
    public int DurationInSeconds { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt => CreatedAt.AddSeconds(DurationInSeconds);
}
