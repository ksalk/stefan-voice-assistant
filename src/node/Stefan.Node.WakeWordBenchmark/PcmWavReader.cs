using System.Text;
using Stefan.Node.Audio;

namespace Stefan.Node.WakeWordBenchmark;

public sealed record PcmWav(byte[] Data, AudioFormat Format);

public static class PcmWavReader
{
    public static PcmWav Read(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return Read(stream, filePath);
        }
        finally
        {
            stream.Dispose();
        }
    }

    public static PcmWav Read(Stream stream, string filePath)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        ReadMarker(reader, "RIFF");
        var riffSize = reader.ReadUInt32();
        _ = riffSize;
        ReadMarker(reader, "WAVE");

        byte[]? data = null;
        ushort channels = 0;
        int sampleRate = 0;
        ushort bitsPerSample = 0;
        var hasFormat = false;

        while (stream.Length - stream.Position >= 8)
        {
            var chunkIdBytes = reader.ReadBytes(4);
            var chunkId = Encoding.UTF8.GetString(chunkIdBytes);
            var declaredChunkSize = reader.ReadUInt32();
            var availableBytes = stream.Length - stream.Position;
            if (availableBytes <= 0)
            {
                break;
            }
            var remainingChunkBytes = (int)Math.Min((uint)declaredChunkSize, availableBytes);

            if (chunkId == "fmt ")
            {
                var fmtBytes = reader.ReadBytes(remainingChunkBytes);
                if (fmtBytes.Length < 16)
                {
                    throw new InvalidDataException("WAV fmt chunk is too short for PCM data.");
                }

                var formatCode = BitConverter.ToUInt16(fmtBytes, 0);
                if (formatCode != 1)
                {
                    throw new InvalidDataException($"Only uncompressed PCM WAV files are supported. Got format code {formatCode}.");
                }

                channels = BitConverter.ToUInt16(fmtBytes, 2);
                sampleRate = BitConverter.ToInt32(fmtBytes, 4);
                bitsPerSample = BitConverter.ToUInt16(fmtBytes, 14);
                hasFormat = true;
            }
            else if (chunkId == "data")
            {
                if (!hasFormat)
                {
                    throw new InvalidDataException("WAV data chunk appeared before the fmt chunk.");
                }
                data = reader.ReadBytes(remainingChunkBytes);
            }
            else
            {
                reader.BaseStream.Seek(remainingChunkBytes, SeekOrigin.Current);
            }
        }

        if (data is null || data.Length == 0)
        {
            throw new InvalidDataException($"WAV file does not contain PCM data: '{filePath}'.");
        }

        if (!hasFormat)
        {
            throw new InvalidDataException($"WAV file does not contain an fmt chunk: '{filePath}'.");
        }

        return new PcmWav(data, new AudioFormat
        {
            Channels = channels,
            SampleRate = sampleRate,
            BitsPerSample = bitsPerSample,
        });
    }

    private static void ReadMarker(BinaryReader reader, string expected)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4 || string.CompareOrdinal(Encoding.UTF8.GetString(bytes), 0, expected, 0, 4) != 0)
        {
            throw new InvalidDataException($"WAV file header does not start with the expected marker '{expected}'.");
        }
    }
}
