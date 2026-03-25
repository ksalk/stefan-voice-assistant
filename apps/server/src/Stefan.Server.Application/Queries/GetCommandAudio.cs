using Microsoft.EntityFrameworkCore;
using Stefan.Server.Application.Services;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Queries;

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

public class GetCommandAudio(
    StefanDbContext dbContext,
    AudioConverterService audioConverter)
{
    public async Task<GetCommandAudioResult?> Handle(GetCommandAudioRequest request, CancellationToken cancellationToken)
    {
        var command = await dbContext.CommandRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.CommandId, cancellationToken);

        if (command == null)
        {
            return null;
        }

        var compressedAudio = request.Type == AudioType.Request
            ? command.InputAudio
            : command.OutputAudio;

        var audioFormat = request.Type == AudioType.Request
            ? command.InputAudioFormat
            : command.OutputAudioFormat;

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

        var fileName = request.Type == AudioType.Request
            ? $"command_{request.CommandId}_request.wav"
            : $"command_{request.CommandId}_response.wav";

        return new GetCommandAudioResult
        {
            AudioBytes = wavBytes,
            ContentType = "audio/wav",
            FileName = fileName,
        };
    }
}
