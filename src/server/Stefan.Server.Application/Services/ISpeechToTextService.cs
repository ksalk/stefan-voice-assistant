namespace Stefan.Server.Application.Services;

public interface ISpeechToTextService
{
    Task<string> TranscribeAsync(Stream audioStream);
}
