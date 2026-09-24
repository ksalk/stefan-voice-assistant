using System.Buffers.Binary;

namespace Stefan.Server.Application.Services;

public static class WavAudio
{
    private const int HeaderSize = 44;

    public static double GetDurationMs(byte[] wavBytes)
    {
        if (wavBytes.Length < HeaderSize) return 0;

        var channels = BinaryPrimitives.ReadUInt16LittleEndian(wavBytes.AsSpan(22));
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(wavBytes.AsSpan(24));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(wavBytes.AsSpan(34));

        if (channels == 0 || sampleRate == 0 || bitsPerSample == 0) return 0;

        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(wavBytes.AsSpan(40));
        var byteRate = sampleRate * channels * (bitsPerSample / 8);

        return byteRate > 0 ? (double)dataSize / byteRate * 1000 : 0;
    }
}
