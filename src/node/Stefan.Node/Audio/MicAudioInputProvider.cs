using System.Threading.Channels;
using Alsa.Net;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

public class MicAudioInputProvider(IOptions<AudioOptions> audioOptions) : IAudioInputProvider
{
    public Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        var input = audioOptions.Value.Input;
        var soundDeviceSettings = new SoundDeviceSettings()
        {
            RecordingDeviceName = input.DeviceName,
            RecordingSampleRate = (uint)input.SampleRate,
            RecordingChannels = (ushort)input.Channels,
            RecordingBitsPerSample = (ushort)input.BitsPerSample
        };

        using var alsaDevice = AlsaDeviceBuilder.Create(soundDeviceSettings);
        try
        {
            alsaDevice.Record(audioBytes =>
            {
                try
                {
                    var chunk = new RawPcmChunk
                    {
                        Bytes = audioBytes,
                        Format = new AudioFormat
                        {
                            Channels = input.Channels,
                            SampleRate = input.SampleRate,
                            BitsPerSample = input.BitsPerSample
                        }
                    };
                    var output = AudioProcessing.ConvertAndResample(chunk, input.ProcessingSampleRate);
                    audioWriter.TryWrite(output.Bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ALSA record callback: {ex}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
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
