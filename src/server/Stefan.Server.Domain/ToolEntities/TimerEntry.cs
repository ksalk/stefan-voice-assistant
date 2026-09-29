using System.Text.Json.Serialization;

namespace Stefan.Server.Domain.ToolEntities;

public class TimerEntry : IToolDocument
{
    public static string DocumentType => "timer";

    public Guid Id { get; set; }
    public int DurationInSeconds { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public DateTime ExpiresAt => CreatedAt.AddSeconds(DurationInSeconds);

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
