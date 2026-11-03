using Microsoft.EntityFrameworkCore;
using Stefan.Server.Application.Services;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class GetCommandAudioRequest
{
    public Guid CommandId { get; set; }
    public AudioType Type { get; set; }
}

public class GetCommandAudioResult
{
    public byte[] AudioBytes { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string FileName { get; set; } = null!;
}

public enum AudioType
{
    Request,
    Response,
    WakeWord
}

public class GetCommandAudio(
    StefanDbContext dbContext,
    AudioConverterService audioConverter)
{
    public async Task<Result<GetCommandAudioResult>> Handle(GetCommandAudioRequest request, CancellationToken cancellationToken)
    {
        var command = await dbContext.CommandRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.CommandId, cancellationToken);

        if (command == null)
        {
            return Result<GetCommandAudioResult>.Failure(Error.NotFound("Command not found"));
        }

        var (compressedAudio, audioFormat, fileNameSuffix) = request.Type switch
        {
            AudioType.Request => (command.InputAudio, command.InputAudioFormat, "request"),
            AudioType.Response => (command.OutputAudio, command.OutputAudioFormat, "response"),
            AudioType.WakeWord => (command.WakeWordAudio, command.WakeWordAudioFormat, "wakeword"),
            _ => (null, null, null),
        };

        if (compressedAudio == null || string.IsNullOrEmpty(audioFormat))
        {
            return Result<GetCommandAudioResult>.Failure(Error.NotFound("Audio not available for this command"));
        }

        // Decompress Opus audio to WAV for browser playback
        byte[] wavBytes;
        try
        {
            wavBytes = await audioConverter.DecompressFromOpusAsync(compressedAudio, cancellationToken);
        }
        catch
        {
            // If decompression fails, return the raw audio
            wavBytes = compressedAudio;
        }

        var fileName = $"command_{request.CommandId}_{fileNameSuffix}.wav";

        return Result<GetCommandAudioResult>.Success(new GetCommandAudioResult
        {
            AudioBytes = wavBytes,
            ContentType = "audio/wav",
            FileName = fileName,
        });
    }
}
