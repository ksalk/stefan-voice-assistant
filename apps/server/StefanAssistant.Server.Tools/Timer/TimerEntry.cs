namespace StefanAssistant.Server.Tools.Timer;

public class TimerEntry
{
    public int Id { get; set; }
    public int Seconds { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
}
