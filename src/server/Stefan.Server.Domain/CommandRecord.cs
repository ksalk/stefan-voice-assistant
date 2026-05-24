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

    public CommandStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public void SaveTranscriptionResult(string transcript, double sttDurationMs)
    {
        Transcript = transcript;
        SttDurationMs = sttDurationMs;
        Status = CommandStatus.SttSuccess;
    }

    public void SaveTranscriptionError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = CommandStatus.SttFailed;
    }

    public void SaveLlmResult(string responseText, string llmConversationJson, double llmDurationMs)
    {
        ResponseText = responseText;
        LlmConversationJson = llmConversationJson;
        LlmDurationMs = llmDurationMs;
        Status = CommandStatus.LlmSuccess;
    }

    public void SaveLlmError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = CommandStatus.LlmFailed;
    }

    public void SaveTtsResult(byte[] outputAudio, string outputAudioFormat, double ttsDurationMs)
    {
        OutputAudio = outputAudio;
        OutputAudioFormat = outputAudioFormat;
        TtsDurationMs = ttsDurationMs;
        Status = CommandStatus.TtsSuccess;
    }

    public void SaveTtsError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = CommandStatus.TtsFailed;
    }

    public void SetTotalDuration(double totalDurationMs)
    {
        TotalDurationMs = totalDurationMs;
    }
}

public enum CommandStatus
{
    Received,
    SttFailed,
    SttSuccess,
    LlmFailed,
    LlmSuccess,
    TtsFailed,
    TtsSuccess,
    HttpFailed,
    Completed,
    Failed
}