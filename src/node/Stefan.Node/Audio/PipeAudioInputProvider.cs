using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

public class PipeAudioInputProvider(IOptions<AudioOptions> audioOptions) : IAudioInputProvider
{
    private const int ChunkSize = 4096;

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string pathname, uint mode);

    public async Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        var pipePath = audioOptions.Value.PipePath ?? "/tmp/audio-input";
        var input = audioOptions.Value.Input;

        Console.WriteLine($"[pipe] Setting up named pipe: {pipePath}");

        try
        {
            if (!File.Exists(pipePath))
            {
                // Permissions need to be set in octal format, e.g. 0666 for read/write permissions for everyone
                var result = mkfifo(pipePath, Convert.ToUInt32("666", 8));
                if (result != 0)
                {
                    throw new IOException($"Failed to create named pipe at {pipePath}. Error code: {result}");
                }
                chmod(pipePath, Convert.ToUInt32("666", 8));
                Console.WriteLine($"[pipe] Named pipe created: {pipePath}");
            }

            Console.WriteLine($"[pipe] Waiting for writer to open pipe: {pipePath}");

            await using var pipeStream = new FileStream(pipePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: ChunkSize, useAsync: true);

            Console.WriteLine("[pipe] Writer connected, reading audio data");

            var buffer = new byte[ChunkSize];
            int bytesRead;

            while ((bytesRead = await pipeStream.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken)) > 0)
            {
                var chunk = buffer.AsSpan(0, bytesRead);
                var monoPcm = ConvertToMono(chunk, input.Channels);
                var resampled = Resample(monoPcm, input.SampleRate, input.ProcessingSampleRate);
                var outputBytes = ConvertSamplesToBytes(resampled);
                await audioWriter.WriteAsync(outputBytes, cancellationToken);
            }

            Console.WriteLine("[pipe] Pipe closed by sender");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[pipe] Reading cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[pipe] Error reading from pipe: {ex}");
        }
        finally
        {
            audioWriter.Complete();
        }
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
