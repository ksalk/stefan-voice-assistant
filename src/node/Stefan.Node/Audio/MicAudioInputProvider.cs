using System.Buffers.Binary;
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
                    var monoPcm = ConvertToMono(audioBytes.AsSpan(), input.Channels);
                    var resampled = Resample(monoPcm, input.SampleRate, input.ProcessingSampleRate);
                    var outputBytes = ConvertSamplesToBytes(resampled);
                    audioWriter.TryWrite(outputBytes);
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

    private static short[] ConvertToMono(ReadOnlySpan<byte> audioBytes, int channels)
    {
        if (channels == 1)
        {
            var sampleCount = audioBytes.Length / 2;
            var mono = new short[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                mono[i] = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(i * 2, 2));
            }
            return mono;
        }

        var frameCount = audioBytes.Length / (2 * channels);
        var monoSamples = new short[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0;
            for (var ch = 0; ch < channels; ch++)
            {
                var offset = (frame * channels + ch) * 2;
                sum += BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(offset, 2));
            }
            monoSamples[frame] = (short)(sum / channels);
        }
        return monoSamples;
    }

    private static short[] Resample(short[] input, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate)
        {
            return input;
        }

        var ratio = (double)sourceRate / targetRate;
        var outputLength = (int)(input.Length / ratio);
        var output = new short[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var srcPos = i * ratio;
            var srcIndex = (int)srcPos;
            var frac = srcPos - srcIndex;

            if (srcIndex + 1 < input.Length)
            {
                var interpolated = input[srcIndex] * (1.0 - frac) + input[srcIndex + 1] * frac;
                output[i] = (short)Math.Clamp(interpolated, short.MinValue, short.MaxValue);
            }
            else
            {
                output[i] = input[srcIndex];
            }
        }

        return output;
    }

    private static byte[] ConvertSamplesToBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        }
        return bytes;
    }
}
