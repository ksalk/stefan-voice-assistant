namespace Stefan.Server.Application.Services;

public interface ITextToSpeechService
{
    Task<Result<TextToSpeechResult>> SynthesizeAsync(string text);
}

public record struct TextToSpeechResult(byte[] AudioBytes, double DurationMs);
