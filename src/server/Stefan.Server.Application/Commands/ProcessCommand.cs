using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class ProcessCommandRequest
{
    public required Guid CommandId { get; set; }
    public required string DeviceId { get; set; }
    public required string SessionId { get; set; }
    public required Stream AudioStream { get; set; }
}

public class ProcessCommand(
    ISpeechToTextService stt,
    LlmCommandService llm,
    ITextToSpeechService tts,
    AudioConverterService audioConverter,
    StefanDbContext dbContext,
    ILogger<ProcessCommand> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task<Result<ProcessCommandResponse>> Handle(ProcessCommandRequest request, CancellationToken cancellationToken)
    {
        // TODO: also extract some methods to make it shorter
        var totalTimestamp = Stopwatch.GetTimestamp();

        using var correlationScope = logger.BeginScope(
            new Dictionary<string, object> { [Correlation.CommandIdProperty] = request.CommandId });

        logger.LogInformation(
            "Processing command {CommandId} for device {DeviceId}, session {SessionId}",
            request.CommandId, request.DeviceId, request.SessionId);

        var node = await ValidateNodeAndSession(request.DeviceId, request.SessionId, cancellationToken);
        if (node == null)
        {
            logger.LogWarning("Command rejected: device {DeviceId} not registered or invalid session", request.DeviceId);
            return Result<ProcessCommandResponse>.Failure(Error.Unauthorized("Unknown device or invalid session"));
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
        var commandRecord = await CreateAndSaveCommandRecord(request.CommandId, node.Id, request.SessionId, compressedInputAudio, inputAudioDurationMs, cancellationToken);

        // STT
        try
        {
            // TODO: maybe pass inputWavBytes directly to avoid creating another MemoryStream, but need to check if stt.TranscribeAsync can read from the same byte array without issues
            using var sttStream = new MemoryStream(inputWavBytes);
            var speechToTextResult = await stt.TranscribeAsync(sttStream);
            if(!speechToTextResult.IsSuccess)
            {
                throw new Exception(speechToTextResult.Error?.Message ?? "Unknown STT error");
            }
            
            var speechToTextTranscription = speechToTextResult.Value;
            if(string.IsNullOrWhiteSpace(speechToTextTranscription.Transcript))
            {
                logger.LogWarning("STT produced empty transcript");
                throw new Exception("STT produced empty transcript");
            }

            commandRecord.SaveTranscriptionResult(speechToTextTranscription.Transcript, speechToTextTranscription.DurationMs);
            


            logger.LogInformation("Transcription result: {Transcript}", speechToTextTranscription.Transcript);
            logger.LogInformation("Speech processing time: {SttDurationMs} ms", speechToTextTranscription.DurationMs);
        }
        catch (Exception ex)
        {
            commandRecord.SaveTranscriptionError(ex.Message);
            logger.LogError(ex, "STT failed: {Error}", ex.Message);

            // TODO: return more detailed error response to client
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<ProcessCommandResponse>.Failure(Error.External("Speech recognition failed"));
        }

        // LLM
        try
        {
            var llmResult = await llm.ProcessCommandAsync(commandRecord.Transcript!, request.DeviceId, cancellationToken);
            if (!llmResult.IsSuccess)
            {
                throw new Exception(llmResult.Error?.Message ?? "Unknown LLM error");
            }
            var result  = llmResult.Value;
            if(string.IsNullOrWhiteSpace(result.ResponseText))
            {
                logger.LogWarning("LLM produced empty response");
                throw new Exception("LLM produced empty response");
            }

            logger.LogInformation("LLM processing time: {LlmDurationMs} ms", result.DurationMs);

            commandRecord.SaveLlmResult(result.ResponseText, JsonSerializer.Serialize(result.Messages, JsonOptions), result.DurationMs);
        }
        catch (ClientResultException ex)
        {
            // This will print the exact OpenRouter validation error (e.g., "message.tool_calls is missing")
            var llmError =  ex.GetRawResponse()?.Content?.ToString();
            logger.LogError(ex, "LLM failed: {Error} {RawResponse}", ex.Message, llmError);

            commandRecord.SaveLlmError(ex.Message + " " + llmError);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<ProcessCommandResponse>.Failure(Error.External("Language model failed"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM failed: {Error}", ex.Message);
            commandRecord.SaveLlmError(ex.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<ProcessCommandResponse>.Failure(Error.External("Language model failed"));
        }

        // TTS
        byte[] wavOutputAudio;
        try
        {
            var ttsResult = await tts.SynthesizeAsync(commandRecord.ResponseText!);
            if (!ttsResult.IsSuccess)
            {
                throw new Exception(ttsResult.Error?.Message ?? "Unknown TTS error");
            }

            wavOutputAudio = ttsResult.Value.AudioBytes;
            var compressedOutputAudio = await CompressAudio(wavOutputAudio, cancellationToken);

            logger.LogInformation(
                "TTS synthesis time: {TtsDurationMs} ms, compressed size: {CompressedSize} bytes",
                ttsResult.Value.DurationMs, compressedOutputAudio.Length);
        
            commandRecord.SaveTtsResult(compressedOutputAudio, "opus", ttsResult.Value.DurationMs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TTS failed: {Error}", ex.Message);
            commandRecord.SaveTtsError(ex.Message);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<ProcessCommandResponse>.Failure(Error.External("Speech synthesis failed"));
        }

        node.MarkSeen();

        var totalDurationMs = Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds;
        commandRecord.SetTotalDuration(totalDurationMs);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Command {CommandId} completed in {TotalDurationMs} ms", request.CommandId, totalDurationMs);

        return Result<ProcessCommandResponse>.Success(
            new ProcessCommandResponse { AudioBytes = wavOutputAudio, ResponseText = commandRecord.ResponseText! });
    }    private async Task<Node?> ValidateNodeAndSession(string deviceId, string sessionId, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes.FirstOrDefaultAsync(n => n.Name == deviceId, cancellationToken);
        if (node == null)
        {
            logger.LogWarning("Command rejected: device {DeviceId} not registered", deviceId);
            return null;
        }

        if (node.CurrentSessionId != sessionId)
        {
            logger.LogWarning("Command rejected: invalid session {SessionId} for device {DeviceId}", sessionId, deviceId);
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
            logger.LogWarning(ex, "Audio compression failed, falling back to uncompressed audio");
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

    private async Task<CommandRecord> CreateAndSaveCommandRecord(Guid commandId, Guid nodeId, string sessionId,
        byte[] inputAudio, double inputAudioDurationMs, CancellationToken cancellationToken)
    {
        var record = new CommandRecord
        {
            Id = commandId,
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
