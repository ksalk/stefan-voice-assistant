using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.Options;
using Stefan.Node.Services;

/// <summary>
/// Capture mode: records incoming audio until shutdown. Runs instead of
/// VoiceCommandDispatcher when Audio:Recording:Enabled is set, so no wake-word
/// detection or command handling happens while this service is active.
/// </summary>
public class AudioRecordingService(
    IAudioInputProvider audioInputProvider,
    AudioRecorder audioRecorder,
    AppStateService appStateService,
    IOptions<AudioOptions> audioOptions,
    ILogger<AudioRecordingService> logger) : BackgroundService
{
    private AudioFormat ProcessingAudioFormat => new AudioFormat
    {
        Channels = 1,
        SampleRate = audioOptions.Value.Input.ProcessingSampleRate,
        BitsPerSample = 16
    };

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        appStateService.CurrentState = VoiceAssistantState.RecordingInput;
        logger.LogInformation("[recorder] Capture mode enabled. Recording input until shutdown; wake-word detection and command processing are off.");

        try
        {
            var audioChannel = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true
                });

            var recordMicTask = Task.Factory.StartNew(
                () => audioInputProvider.WriteAudioInput(audioChannel.Writer, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            await foreach (var audioBytes in audioChannel.Reader.ReadAllAsync(cancellationToken))
            {
                audioRecorder.WriteProcessed(new RawPcmChunk { Bytes = audioBytes, Format = ProcessingAudioFormat });
            }

            await recordMicTask;
        }
        catch (OperationCanceledException)
        {
            throw; // Let BackgroundService handle graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[recorder] Fatal error while recording input");
            throw;
        }
    }
}
