using System.Text;
using Stefan.Node.WakeWordBenchmark;

namespace Stefan.Node.Tests;

public sealed class PcmWavReaderTests
{
    [Fact]
    public void Read_ParsesStandardPcmWav()
    {
        using var stream = new MemoryStream(BuildPcmWav());
        var pcm = PcmWavReader.Read(stream, "test.wav");

        Assert.Equal(1, pcm.Format.Channels);
        Assert.Equal(16000, pcm.Format.SampleRate);
        Assert.Equal(16, pcm.Format.BitsPerSample);
        Assert.Equal(2, pcm.Data.Length);
    }

    [Fact]
    public void Read_RejectsNonPcmWav()
    {
        using var stream = new MemoryStream(BuildNonPcmWav());
        Assert.Throws<InvalidDataException>(() => PcmWavReader.Read(stream, "test.wav"));
    }

    private static byte[] BuildPcmWav()
    {
        var bytes = new List<byte>(44 + 2);
        bytes.AddRange("RIFF"u8);
        bytes.AddRange(BitConverter.GetBytes(36 + 2));
        bytes.AddRange("WAVE"u8);

        bytes.AddRange("fmt "u8);
        bytes.AddRange(BitConverter.GetBytes(16));
        bytes.AddRange(BitConverter.GetBytes(1));
        bytes.AddRange(BitConverter.GetBytes(1));
        bytes.AddRange(BitConverter.GetBytes(16000));
        bytes.AddRange(BitConverter.GetBytes(32000));
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.AddRange(BitConverter.GetBytes(16));

        bytes.AddRange("data"u8);
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.AddRange(new byte[2]);
        return bytes.ToArray();
    }

    private static byte[] BuildNonPcmWav()
    {
        var bytes = new List<byte>(44 + 2);
        bytes.AddRange("RIFF"u8);
        bytes.AddRange(BitConverter.GetBytes(36 + 2));
        bytes.AddRange("WAVE"u8);

        bytes.AddRange("fmt "u8);
        bytes.AddRange(BitConverter.GetBytes(16));
        bytes.AddRange(BitConverter.GetBytes(0));
        bytes.AddRange(BitConverter.GetBytes(1));
        bytes.AddRange(BitConverter.GetBytes(16000));
        bytes.AddRange(BitConverter.GetBytes(32000));
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.AddRange(BitConverter.GetBytes(16));

        bytes.AddRange("data"u8);
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.AddRange(new byte[2]);
        return bytes.ToArray();
    }
}
