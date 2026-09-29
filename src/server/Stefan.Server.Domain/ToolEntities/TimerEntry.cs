using System.Text.Json.Serialization;

namespace Stefan.Server.Domain.ToolEntities;

[ToolDocumentType("timer")]
public class TimerEntry
{
    public Guid Id { get; set; }
    public int DurationInSeconds { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public DateTime ExpiresAt => CreatedAt.AddSeconds(DurationInSeconds);

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
