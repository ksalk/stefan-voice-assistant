using System.Threading.Channels;
using Alsa.Net;

namespace Stefan.Node.Audio;

public class MicAudioInputProvider : IAudioInputProvider
{
    private const int InputSampleRate = 16_000;

    public Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        // TODO: Make sound device settings configurable and discover devices
        var soundDeviceSettings = new SoundDeviceSettings()
        {
            RecordingDeviceName = "plughw:1,0", // software resampling if hw doesn't support 16kHz natively
            RecordingSampleRate = InputSampleRate,
            RecordingChannels = 2,
            RecordingBitsPerSample = 16
        };

        using var alsaDevice = AlsaDeviceBuilder.Create(soundDeviceSettings);
        try
        {
            alsaDevice.Record(audioBytes =>
            {
                try
                {
                    // Need to copy the bytes since AlsaDevice reuses the same buffer for each callback
                    audioWriter.TryWrite(audioBytes.ToArray());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ALSA record callback: {ex}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ALSA record loop: {ex}");
        }
        finally
        {
            audioWriter.Complete();
        }

        return Task.CompletedTask;
    }
}