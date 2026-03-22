using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Domain;
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
    AudioConverterService audioConverter,
    StefanDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task<ProcessCommandResponse?> Handle(ProcessCommandRequest request, CancellationToken cancellationToken)
    {
        var totalTimestamp = Stopwatch.GetTimestamp();

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

        // Buffer the input audio stream so we can read it twice (STT + storage)
        using var audioBuffer = new MemoryStream();
        await request.AudioStream.CopyToAsync(audioBuffer, cancellationToken);
        var inputWavBytes = audioBuffer.ToArray();
        var inputAudioDurationMs = GetWavDurationMs(inputWavBytes);

        byte[] compressedInputAudio;
        string transcript;
        LlmCommandResult? llmResult = null;
        byte[] audioBytes = [];
        string responseText = "Error";
        string status = "Success";
        string? errorMessage = null;
        double sttDurationMs = 0, llmDurationMs = 0, ttsDurationMs = 0;

        // Compress input audio to Opus
        try
        {
            compressedInputAudio = await audioConverter.CompressToOpusAsync(inputWavBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Audio compression failed for input: {ex.Message}");
            compressedInputAudio = inputWavBytes;
        }

        // STT
        try
        {
            var timestamp = Stopwatch.GetTimestamp();

            using var sttStream = new MemoryStream(inputWavBytes);
            transcript = await stt.TranscribeAsync(sttStream);

            sttDurationMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            ConsoleLog.Write(LogCategory.STT, $"Transcription result: {transcript}");
            ConsoleLog.Write(LogCategory.STT, $"Speech processing time: {sttDurationMs} ms");
        }
        catch (Exception ex)
        {
            status = "SttFailed";
            errorMessage = ex.Message;
            ConsoleLog.Write(LogCategory.STT, $"STT failed: {ex.Message}");

            await SaveRecordAsync(node.Id, request.SessionId, compressedInputAudio, inputAudioDurationMs,
                "[STT failed]", "[]", "Error", [],
                sttDurationMs, llmDurationMs, ttsDurationMs,
                Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds,
                status, errorMessage, cancellationToken);

            return null;
        }

        // LLM
        try
        {
            var timestamp = Stopwatch.GetTimestamp();

            llmResult = await llm.ProcessCommandAsync(transcript, request.DeviceId, cancellationToken);
            responseText = llmResult.ResponseText;

            llmDurationMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            ConsoleLog.Write(LogCategory.LLM, $"LLM processing time: {llmDurationMs} ms");
        }
        catch (Exception ex)
        {
            status = "LlmFailed";
            errorMessage = ex.Message;
            ConsoleLog.Write(LogCategory.LLM, $"LLM failed: {ex.Message}");

            await SaveRecordAsync(node.Id, request.SessionId, compressedInputAudio, inputAudioDurationMs,
                transcript, JsonSerializer.Serialize(llmResult?.Messages ?? [], JsonOptions), "Error", [],
                sttDurationMs, llmDurationMs, ttsDurationMs,
                Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds,
                status, errorMessage, cancellationToken);

            return null;
        }

        // TTS
        try
        {
            var timestamp = Stopwatch.GetTimestamp();

            audioBytes = await tts.SynthesizeAsync(responseText);

            ttsDurationMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            ConsoleLog.Write(LogCategory.TTS, $"TTS synthesis time: {ttsDurationMs} ms, size: {audioBytes.Length} bytes");
        }
        catch (Exception ex)
        {
            status = "TtsFailed";
            errorMessage = ex.Message;
            ConsoleLog.Write(LogCategory.TTS, $"TTS failed: {ex.Message}");
        }

        // Compress output audio
        byte[] compressedOutputAudio;
        try
        {
            compressedOutputAudio = audioBytes.Length > 0
                ? await audioConverter.CompressToOpusAsync(audioBytes, cancellationToken)
                : [];
        }
        catch (Exception ex)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Audio compression failed for output: {ex.Message}");
            compressedOutputAudio = audioBytes;
        }

        node.MarkSeen();

        var conversationJson = JsonSerializer.Serialize(llmResult?.Messages ?? [], JsonOptions);
        var totalDurationMs = Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds;

        await SaveRecordAsync(node.Id, request.SessionId, compressedInputAudio, inputAudioDurationMs,
            transcript, conversationJson, responseText, compressedOutputAudio,
            sttDurationMs, llmDurationMs, ttsDurationMs, totalDurationMs,
            status, errorMessage, cancellationToken);

        return new ProcessCommandResponse { AudioBytes = audioBytes, ResponseText = responseText };
    }

    private static double GetWavDurationMs(byte[] wavBytes)
    {
        if (wavBytes.Length < 44) return 0;

        // PCM WAV: header is 44 bytes. Data size is at offset 40 (uint32 LE).
        // Sample rate at offset 24 (uint32 LE), bits per sample at offset 34 (uint16 LE), channels at offset 22 (uint16 LE).
        var channels = BitConverter.ToUInt16(wavBytes, 22);
        var sampleRate = BitConverter.ToUInt32(wavBytes, 24);
        var bitsPerSample = BitConverter.ToUInt16(wavBytes, 34);

        if (channels == 0 || sampleRate == 0 || bitsPerSample == 0) return 0;

        var dataSize = BitConverter.ToUInt32(wavBytes, 40);
        var byteRate = sampleRate * channels * (bitsPerSample / 8);

        return byteRate > 0 ? (double)dataSize / byteRate * 1000 : 0;
    }

    private async Task SaveRecordAsync(
        Guid nodeId, string sessionId,
        byte[] inputAudio, double inputAudioDurationMs, string transcript, string conversationJson, string responseText,
        byte[] outputAudio,
        double sttMs, double llmMs, double ttsMs, double totalMs,
        string status, string? errorMessage,
        CancellationToken cancellationToken)
    {
        var record = new CommandRecord
        {
            Id = Guid.NewGuid(),
            NodeId = nodeId,
            SessionId = sessionId,
            ReceivedAt = DateTime.UtcNow,
            InputAudio = inputAudio,
            InputAudioFormat = "opus",
            InputAudioDurationMs = inputAudioDurationMs,
            Transcript = transcript,
            LlmConversationJson = conversationJson,
            ResponseText = responseText,
            OutputAudio = outputAudio,
            OutputAudioFormat = "opus",
            SttDurationMs = sttMs,
            LlmDurationMs = llmMs,
            TtsDurationMs = ttsMs,
            TotalDurationMs = totalMs,
            Status = status,
            ErrorMessage = errorMessage,
        };

        dbContext.CommandRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        ConsoleLog.Write(LogCategory.HTTP, $"CommandRecord saved: {record.Id}");
    }
}

public class ProcessCommandResponse
{
    public byte[] AudioBytes { get; set; }
    public string ResponseText { get; set; }
}
