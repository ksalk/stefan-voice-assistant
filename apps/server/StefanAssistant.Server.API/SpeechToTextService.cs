using Whisper.net;

namespace StefanAssistant.Server.API;

public class SpeechToTextService(WhisperProcessor processor)
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
