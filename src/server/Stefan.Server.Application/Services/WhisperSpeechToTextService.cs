using Whisper.net;

namespace Stefan.Server.Application.Services;

public class WhisperSpeechToTextService(WhisperProcessor processor) : ISpeechToTextService
{
    public async Task<string> TranscribeAsync(Stream audioStream)
    {
        var segments = new List<string>();

        await foreach (var segment in processor.ProcessAsync(audioStream))
        {
            segments.Add(segment.Text);
        }

        return string.Concat(segments).Trim();
    }
}
