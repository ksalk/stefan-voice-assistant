using System.Buffers.Binary;

namespace Stefan.Node.Audio;

public static class AudioProcessing
{
    public static short[] ConvertToMono(ReadOnlySpan<byte> audioBytes, int channels)
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

    public static short[] Resample(short[] input, int sourceRate, int targetRate)
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

    public static byte[] ConvertSamplesToBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        }
        return bytes;
    }

    public static byte[] ConvertAndResample(ReadOnlySpan<byte> audioBytes, int channels, int sourceRate, int targetRate)
    {
        var monoPcm = ConvertToMono(audioBytes, channels);
        var resampled = Resample(monoPcm, sourceRate, targetRate);
        return ConvertSamplesToBytes(resampled);
    }
}
