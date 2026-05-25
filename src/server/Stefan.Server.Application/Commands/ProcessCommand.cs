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
    public required string DeviceId { get; set; }
    public required string SessionId { get; set; }
    public required Stream AudioStream { get; set; }
}

public class ProcessCommand(
    ISpeechToTextService stt,
    LlmCommandService llm,
    ITextToSpeechService tts,
    AudioConverterService audioConverter,
    StefanDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task<ProcessCommandResponse?> Handle(ProcessCommandRequest request, CancellationToken cancellationToken)
    {
        // TODO: also extract some methods to make it shorter
        var totalTimestamp = Stopwatch.GetTimestamp();

        var node = await ValidateNodeAndSession(request.DeviceId, request.SessionId, cancellationToken);
        if (node == null)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: device '{request.DeviceId}' not registered or invalid session");
            return null;
        }

        // TODO: is this really required, is audio read twice at all?
        // Buffer the input audio stream so we can read it twice (STT + storage)
        using var audioBuffer = new MemoryStream();
        await request.AudioStream.CopyToAsync(audioBuffer, cancellationToken);
        var inputWavBytes = audioBuffer.ToArray();
        var inputAudioDurationMs = GetWavDurationMs(inputWavBytes);

        // Compress input audio to Opus
        var compressedInputAudio = await CompressAudio(inputWavBytes, cancellationToken);

        // Create initial command record with input audio and duration, so we have a record even if STT fails
        var commandRecord = await CreateAndSaveCommandRecord(node.Id, request.SessionId, compressedInputAudio, inputAudioDurationMs, cancellationToken);

        // STT
        try
        {
            // TODO: maybe pass inputWavBytes directly to avoid creating another MemoryStream, but need to check if stt.TranscribeAsync can read from the same byte array without issues
            using var sttStream = new MemoryStream(inputWavBytes);
            var speechToTextResult = await stt.TranscribeAsync(sttStream);
            if(!speechToTextResult.IsSuccess)
            {
                throw new Exception(speechToTextResult.Error ?? "Unknown STT error");
            }
            
            var speechToTextTranscription = speechToTextResult.Value;
            commandRecord.SaveTranscriptionResult(speechToTextTranscription.Transcript, speechToTextTranscription.DurationMs);

            ConsoleLog.Write(LogCategory.STT, $"Transcription result: {speechToTextTranscription.Transcript}");
            ConsoleLog.Write(LogCategory.STT, $"Speech processing time: {speechToTextTranscription.DurationMs} ms");
        }
        catch (Exception ex)
        {
            commandRecord.SaveTranscriptionError(ex.Message);
            ConsoleLog.Write(LogCategory.STT, $"STT failed: {ex.Message}");
    
            // TODO: return more detailed error response to client
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        // LLM
        try
        {
            var llmResult = await llm.ProcessCommandAsync(commandRecord.Transcript, request.DeviceId, cancellationToken);
            if (!llmResult.IsSuccess)
            {
                throw new Exception(llmResult.Error ?? "Unknown LLM error");
            }
            var result  = llmResult.Value;

            ConsoleLog.Write(LogCategory.LLM, $"LLM processing time: {result.DurationMs} ms");

            commandRecord.SaveLlmResult(result.ResponseText, JsonSerializer.Serialize(result.Messages, JsonOptions), result.DurationMs);
        }
        catch (Exception ex)
        {
            ConsoleLog.Write(LogCategory.LLM, $"LLM failed: {ex.Message}");
            commandRecord.SaveLlmError(ex.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        // TTS
        try
        {
            var ttsResult = await tts.SynthesizeAsync(commandRecord.ResponseText);
            if (!ttsResult.IsSuccess)
            {
                throw new Exception(ttsResult.Error ?? "Unknown TTS error");
            }

            var compressedOutputAudio = await CompressAudio(ttsResult.Value.AudioBytes, cancellationToken);

            ConsoleLog.Write(LogCategory.TTS, $"TTS synthesis time: {ttsResult.Value.DurationMs} ms, compressed size: {compressedOutputAudio.Length} bytes");
        
            commandRecord.SaveTtsResult(compressedOutputAudio, "opus", ttsResult.Value.DurationMs);
        }
        catch (Exception ex)
        {
            ConsoleLog.Write(LogCategory.TTS, $"TTS failed: {ex.Message}");
            commandRecord.SaveTtsError(ex.Message);

            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        node.MarkSeen();

        var totalDurationMs = Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds;
        commandRecord.SetTotalDuration(totalDurationMs);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessCommandResponse { AudioBytes = commandRecord.OutputAudio, ResponseText = commandRecord.ResponseText };
    }

    private async Task<Node?> ValidateNodeAndSession(string deviceId, string sessionId, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes.FirstOrDefaultAsync(n => n.Name == deviceId, cancellationToken);
        if (node == null)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: device '{deviceId}' not registered");
            return null;
        }

        if (node.CurrentSessionId != sessionId)
        {
            ConsoleLog.Write(LogCategory.HTTP, $"Command request rejected: invalid session ID for device '{deviceId}'");
            return null;
        }

        return node;
    }

    private async Task<byte[]> CompressAudio(byte[] inputWavBytes, CancellationToken cancellationToken)
    {
        try
        {
            return await audioConverter.CompressToOpusAsync(inputWavBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            // TODO: also log error, but continue processing with uncompressed audio to avoid failing the whole command just because compression failed. We can compress it later when we save the record to db, so at least we have compressed audio stored even if compression fails here.
            ConsoleLog.Write(LogCategory.HTTP, $"Audio compression failed: {ex.Message}");
            return inputWavBytes;
        }
    }

    private static double GetWavDurationMs(byte[] wavBytes)
    {
        if (wavBytes.Length < 44) return 0;

        // PCM WAV: header is 44 bytes. Data size is at offset 40 (uint32 LE).
        // Sample rate at offset 24 (uint32 LE), bits per sample at offset 34 (uint16 LE), channels at offset 22 (uint16 LE).
        // Using BitConverter assumes the system is little-endian. WAV is always little-endian,
        // so this works on x86/ARM but would break on big-endian systems. Use BinaryPrimitives.ReadUInt16LittleEndian for portable code.
        var channels = BitConverter.ToUInt16(wavBytes, 22);
        var sampleRate = BitConverter.ToUInt32(wavBytes, 24);
        var bitsPerSample = BitConverter.ToUInt16(wavBytes, 34);

        if (channels == 0 || sampleRate == 0 || bitsPerSample == 0) return 0;

        var dataSize = BitConverter.ToUInt32(wavBytes, 40);
        var byteRate = sampleRate * channels * (bitsPerSample / 8);

        return byteRate > 0 ? (double)dataSize / byteRate * 1000 : 0;
    }

    private async Task<CommandRecord> CreateAndSaveCommandRecord(Guid nodeId, string sessionId,
        byte[] inputAudio, double inputAudioDurationMs, CancellationToken cancellationToken)
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
            Status = CommandStatus.Received
        };

        await dbContext.CommandRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return record;
    }
}

public class ProcessCommandResponse
{
    public required byte[] AudioBytes { get; set; }
    public required string ResponseText { get; set; }
}
