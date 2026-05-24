namespace Stefan.Server.Application.Services;

public interface ISpeechToTextService
{
    Task<Result<SpeechToTextTranscription>> TranscribeAsync(Stream audioStream);
}

public record struct SpeechToTextTranscription
{
    public string Transcript { get; set; }
    public double DurationMs { get; set; }
}