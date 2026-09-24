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
        var totalTimestamp = Stopwatch.GetTimestamp();

        using var correlationScope = logger.BeginScope(
            new Dictionary<string, object> { [Correlation.CommandIdProperty] = request.CommandId });

        logger.LogInformation(
            "Processing command {CommandId} for device {DeviceId}, session {SessionId}",
            request.CommandId, request.DeviceId, request.SessionId);

        var nodeResult = await ValidateNodeAndSession(request.DeviceId, request.SessionId, cancellationToken);
        if (!nodeResult.IsSuccess)
        {
            return Result<ProcessCommandResponse>.Failure(nodeResult.Error!);
        }

        var node = nodeResult.Value!;

        // TODO: is this really required, is audio read twice at all?
        var inputAudio = await PrepareInputAudioAsync(request.AudioStream, cancellationToken);

        // Create initial command record with input audio and duration, so we have a record even if STT fails
        var commandRecord = await CreateAndSaveCommandRecord(
            request.CommandId,
            node.Id,
            request.SessionId,
            inputAudio.CompressedOpus,
            inputAudio.DurationMs,
            cancellationToken);

        var sttResult = await RunSttAsync(commandRecord, inputAudio.WavBytes, cancellationToken);
        if (!sttResult.IsSuccess)
        {
            return Result<ProcessCommandResponse>.Failure(sttResult.Error!);
        }

        var llmResult = await RunLlmAsync(commandRecord, request.DeviceId, cancellationToken);
        if (!llmResult.IsSuccess)
        {
            return Result<ProcessCommandResponse>.Failure(llmResult.Error!);
        }

        var ttsResult = await RunTtsAsync(commandRecord, cancellationToken);
        if (!ttsResult.IsSuccess)
        {
            return Result<ProcessCommandResponse>.Failure(ttsResult.Error!);
        }

        node.MarkSeen();

        var totalDurationMs = Stopwatch.GetElapsedTime(totalTimestamp).TotalMilliseconds;
        commandRecord.SetTotalDuration(totalDurationMs);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Command {CommandId} completed in {TotalDurationMs} ms", request.CommandId, totalDurationMs);

        return Result<ProcessCommandResponse>.Success(new ProcessCommandResponse
        {
            AudioBytes = ttsResult.Value!,
            ResponseText = commandRecord.ResponseText!,
        });
    }

    private async Task<Result<Node>> ValidateNodeAndSession(string deviceId, string sessionId, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes.FirstOrDefaultAsync(n => n.Name == deviceId, cancellationToken);
        if (node == null)
        {
            logger.LogWarning("Command rejected: device {DeviceId} not registered", deviceId);
            return Result<Node>.Failure(Error.Unauthorized("Unknown device or invalid session"));
        }

        if (node.CurrentSessionId != sessionId)
        {
            logger.LogWarning("Command rejected: invalid session {SessionId} for device {DeviceId}", sessionId, deviceId);
            return Result<Node>.Failure(Error.Unauthorized("Unknown device or invalid session"));
        }

        return Result<Node>.Success(node);
    }

    private async Task<InputAudio> PrepareInputAudioAsync(Stream audioStream, CancellationToken cancellationToken)
    {
        // Buffer the input audio stream so we can read it twice (STT + storage)
        using var audioBuffer = new MemoryStream();
        await audioStream.CopyToAsync(audioBuffer, cancellationToken);

        var wavBytes = audioBuffer.ToArray();
        var durationMs = WavAudio.GetDurationMs(wavBytes);
        var compressedOpus = await CompressAudio(wavBytes, cancellationToken);

        return new InputAudio(wavBytes, compressedOpus, durationMs);
    }

    private async Task<Result<SpeechToTextTranscription>> RunSttAsync(CommandRecord record, byte[] inputWavBytes, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: maybe pass inputWavBytes directly to avoid creating another MemoryStream, but need to check if stt.TranscribeAsync can read from the same byte array without issues
            using var sttStream = new MemoryStream(inputWavBytes);
            var result = await stt.TranscribeAsync(sttStream, cancellationToken);

            if (!result.IsSuccess)
            {
                return await RecordSttFailureAsync(record, result.Error?.Message ?? "Unknown STT error", cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(result.Value.Transcript))
            {
                logger.LogWarning("STT produced empty transcript");
                return await RecordSttFailureAsync(record, "STT produced empty transcript", cancellationToken);
            }

            record.SaveTranscriptionResult(result.Value.Transcript, result.Value.DurationMs);

            logger.LogInformation("Transcription result: {Transcript}", result.Value.Transcript);
            logger.LogInformation("Speech processing time: {SttDurationMs} ms", result.Value.DurationMs);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "STT failed: {Error}", ex.Message);
            return await RecordSttFailureAsync(record, ex.Message, cancellationToken);
        }
    }

    private async Task<Result<SpeechToTextTranscription>> RecordSttFailureAsync(CommandRecord record, string detail, CancellationToken cancellationToken)
    {
        record.SaveTranscriptionError(detail);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SpeechToTextTranscription>.Failure(Error.External($"Speech recognition failed: {detail}"));
    }

    private async Task<Result<LlmCommandResult>> RunLlmAsync(CommandRecord record, string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await llm.ProcessCommandAsync(record.Transcript!, deviceId, cancellationToken);

            if (!result.IsSuccess)
            {
                return await RecordLlmFailureAsync(record, result.Error?.Message ?? "Unknown LLM error", cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(result.Value.ResponseText))
            {
                logger.LogWarning("LLM produced empty response");
                return await RecordLlmFailureAsync(record, "LLM produced empty response", cancellationToken);
            }

            logger.LogInformation("LLM processing time: {LlmDurationMs} ms", result.Value.DurationMs);

            record.SaveLlmResult(result.Value.ResponseText, JsonSerializer.Serialize(result.Value.Messages, JsonOptions), result.Value.DurationMs);

            return result;
        }
        catch (ClientResultException ex)
        {
            // This will print the exact OpenRouter validation error (e.g., "message.tool_calls is missing")
            var rawResponse = ex.GetRawResponse()?.Content?.ToString();
            logger.LogError(ex, "LLM failed: {Error} {RawResponse}", ex.Message, rawResponse);

            return await RecordLlmFailureAsync(record, $"{ex.Message} {rawResponse}", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "LLM failed: {Error}", ex.Message);
            return await RecordLlmFailureAsync(record, ex.Message, cancellationToken);
        }
    }

    private async Task<Result<LlmCommandResult>> RecordLlmFailureAsync(CommandRecord record, string detail, CancellationToken cancellationToken)
    {
        record.SaveLlmError(detail);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LlmCommandResult>.Failure(Error.External($"Language model failed: {detail}"));
    }

    private async Task<Result<byte[]>> RunTtsAsync(CommandRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var result = await tts.SynthesizeAsync(record.ResponseText!, cancellationToken);

            if (!result.IsSuccess)
            {
                return await RecordTtsFailureAsync(record, result.Error?.Message ?? "Unknown TTS error", cancellationToken);
            }

            var wavOutputAudio = result.Value.AudioBytes;
            var compressedOutputAudio = await CompressAudio(wavOutputAudio, cancellationToken);

            logger.LogInformation(
                "TTS synthesis time: {TtsDurationMs} ms, compressed size: {CompressedSize} bytes",
                result.Value.DurationMs, compressedOutputAudio.Length);

            record.SaveTtsResult(compressedOutputAudio, "opus", result.Value.DurationMs);

            return Result<byte[]>.Success(wavOutputAudio);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "TTS failed: {Error}", ex.Message);
            return await RecordTtsFailureAsync(record, ex.Message, cancellationToken);
        }
    }

    private async Task<Result<byte[]>> RecordTtsFailureAsync(CommandRecord record, string detail, CancellationToken cancellationToken)
    {
        record.SaveTtsError(detail);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<byte[]>.Failure(Error.External($"Speech synthesis failed: {detail}"));
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

    private readonly record struct InputAudio(byte[] WavBytes, byte[] CompressedOpus, double DurationMs);
}

public class ProcessCommandResponse
{
    public required byte[] AudioBytes { get; set; }
    public required string ResponseText { get; set; }
}
