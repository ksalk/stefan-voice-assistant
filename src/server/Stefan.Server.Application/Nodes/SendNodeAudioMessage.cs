using Microsoft.Extensions.Logging;
using Stefan.Server.Application.Services;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class SendNodeAudioMessageRequest
{
    public Guid NodeId { get; set; }
    public required string Text { get; set; }
}

public class SendNodeAudioMessageResult
{
    public double TtsDurationMs { get; set; }
}

public class SendNodeAudioMessage(
    StefanDbContext dbContext,
    NodeHttpClient nodeHttpClient,
    ITextToSpeechService tts,
    ILogger<SendNodeAudioMessage> logger)
{
    private const int MaxTextLength = 250;

    public async Task<Result<SendNodeAudioMessageResult>> Handle(SendNodeAudioMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return Result<SendNodeAudioMessageResult>.Failure("Text is required.");

        if (request.Text.Length > MaxTextLength)
            return Result<SendNodeAudioMessageResult>.Failure($"Text must not exceed {MaxTextLength} characters.");

        var node = await dbContext.Nodes.FindAsync([request.NodeId], cancellationToken);
        if (node is null)
            return Result<SendNodeAudioMessageResult>.Failure("Node not found.");

        var ttsResult = await tts.SynthesizeAsync(request.Text);
        if (!ttsResult.IsSuccess)
            return Result<SendNodeAudioMessageResult>.Failure($"Text-to-speech failed: {ttsResult.Error}");

        try
        {
            await nodeHttpClient.SendWavAudio(node, ttsResult.Value.AudioBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send audio to node {NodeName}", node.Name);
            return Result<SendNodeAudioMessageResult>.Failure($"Failed to deliver audio: {ex.Message}");
        }

        return Result<SendNodeAudioMessageResult>.Success(new SendNodeAudioMessageResult
        {
            TtsDurationMs = ttsResult.Value.DurationMs
        });
    }
}
