using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class ProcessCommandRequest
{
    public string DeviceId { get; set; }
    public string SessionId { get; set; }
    public Stream AudioStream { get; set; }
}

public class ProcessCommand(
    ISpeechToTextService stt,
    LlmCommandService llm,
    TextToSpeechService tts,
    StefanDbContext dbContext)
{
    public async Task<ProcessCommandResponse?> Handle(ProcessCommandRequest request, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes.FirstOrDefaultAsync(n => n.Name == request.DeviceId, cancellationToken);
        if (node == null)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: device '{request.DeviceId}' not registered");
            return null;
        }

        if (node.CurrentSessionId != request.SessionId)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: invalid session ID for device '{request.DeviceId}'");
            return null;
        }

        var timestamp = Stopwatch.GetTimestamp();

        string transcript = await stt.TranscribeAsync(request.AudioStream);

        ConsoleLog.Write(LogCategory.STT, $"Transcription result: {transcript}");
        ConsoleLog.Write(LogCategory.STT, $"Speech processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

        timestamp = Stopwatch.GetTimestamp();

        string response = await llm.ProcessCommandAsync(transcript, request.DeviceId, cancellationToken);

        ConsoleLog.Write(LogCategory.LLM, $"LLM processing time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms");

        timestamp = Stopwatch.GetTimestamp();

        byte[] audioBytes = await tts.SynthesizeAsync(response);

        ConsoleLog.Write(LogCategory.TTS, $"TTS synthesis time: {Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds} ms, size: {audioBytes.Length} bytes");

        node.MarkSeen();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessCommandResponse { AudioBytes = audioBytes, ResponseText = response };
    }
}

public class ProcessCommandResponse
{
    public byte[] AudioBytes { get; set; }
    public string ResponseText { get; set; }
}
