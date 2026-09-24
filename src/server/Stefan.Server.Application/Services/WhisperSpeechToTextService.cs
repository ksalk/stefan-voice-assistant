using System.Diagnostics;
using Whisper.net;

namespace Stefan.Server.Application.Services;

public class WhisperSpeechToTextService(WhisperProcessor processor) : ISpeechToTextService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Result<SpeechToTextTranscription>> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var segments = new List<string>();
            var startTimestamp = Stopwatch.GetTimestamp();

            await foreach (var segment in processor.ProcessAsync(audioStream).WithCancellation(cancellationToken))
            {
                segments.Add(segment.Text);
            }

            var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var transcript = string.Concat(segments).Trim();
            return new SpeechToTextTranscription
            {
                Transcript = transcript,
                DurationMs = durationMs
            };
        }
        finally
        {
            _gate.Release();
        }
    }
}
