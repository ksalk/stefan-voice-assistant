namespace Stefan.Server.Domain;

public class CommandRecord
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string SessionId { get; set; } = null!;
    public DateTime ReceivedAt { get; set; }

    public byte[] InputAudio { get; set; } = null!;
    public string InputAudioFormat { get; set; } = null!;
    public double InputAudioDurationMs { get; set; }

    public string Transcript { get; set; } = null!;
    public string LlmConversationJson { get; set; } = null!;
    public string ResponseText { get; set; } = null!;

    public byte[] OutputAudio { get; set; } = null!;
    public string OutputAudioFormat { get; set; } = null!;

    public double SttDurationMs { get; set; }
    public double LlmDurationMs { get; set; }
    public double TtsDurationMs { get; set; }
    public double TotalDurationMs { get; set; }

    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}
