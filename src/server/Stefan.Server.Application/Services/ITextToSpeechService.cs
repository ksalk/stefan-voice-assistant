namespace Stefan.Server.Application.Services;

public interface ITextToSpeechService
{
    Task<byte[]> SynthesizeAsync(string text);
}
