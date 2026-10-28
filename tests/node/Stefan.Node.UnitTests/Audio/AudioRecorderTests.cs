using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.Options;

namespace Stefan.Node.UnitTests.Audio;

public class AudioRecorderTests : IDisposable
{
    private readonly string _baseDirectory = Path.Combine(
        Path.GetTempPath(), "stefan-node-tests", Guid.NewGuid().ToString("D"));

    [Fact]
    public void WriteProcessed_WhenEnabled_WritesWavFileWithHeaderAndSamples()
    {
        // Arrange
        var samples = new short[] { 100, -200, 300, -400 };
        using var recorder = CreateRecorder(new AudioRecordingOptions
        {
            Enabled = true,
            IncludeRaw = false,
            IncludeProcessed = true
        });

        // Act
        recorder.WriteProcessed(MonoChunk(samples, sampleRate: 16000));
        recorder.Dispose();

        // Assert
        var filePath = Directory.GetFiles(_baseDirectory, "mic_processed_*.wav").Single();
        var bytes = File.ReadAllBytes(filePath);

        Assert.Equal(44 + samples.Length * 2, bytes.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes[0..4]));
        Assert.Equal(bytes.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4)));
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22, 2)));
        Assert.Equal(16000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24, 4)));
        Assert.Equal(samples.Length * 2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40, 4)));

        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44 + i * 2, 2)));
        }
    }

    [Fact]
    public void WriteRawAndProcessed_WhenEnabled_WritesTwoFilesInTheirFormats()
    {
        // Arrange
        using var recorder = CreateRecorder(new AudioRecordingOptions { Enabled = true });

        // Act
        recorder.WriteRaw(MonoChunk([500], sampleRate: 24000));
        recorder.WriteProcessed(MonoChunk([-500], sampleRate: 16000));
        recorder.Dispose();

        // Assert
        var rawPath = Directory.GetFiles(_baseDirectory, "mic_raw_*.wav").Single();
        var processedPath = Directory.GetFiles(_baseDirectory, "mic_processed_*.wav").Single();
        var rawBytes = File.ReadAllBytes(rawPath);
        var processedBytes = File.ReadAllBytes(processedPath);

        Assert.Equal(24000, BinaryPrimitives.ReadInt32LittleEndian(rawBytes.AsSpan(24, 4)));
        Assert.Equal(16000, BinaryPrimitives.ReadInt32LittleEndian(processedBytes.AsSpan(24, 4)));
        Assert.Equal(500, BinaryPrimitives.ReadInt16LittleEndian(rawBytes.AsSpan(44, 2)));
        Assert.Equal(-500, BinaryPrimitives.ReadInt16LittleEndian(processedBytes.AsSpan(44, 2)));

        // The sample rate is part of the file name, so benchmark files describe their own format
        Assert.Contains("24000hz", Path.GetFileName(rawPath));
        Assert.Contains("16000hz", Path.GetFileName(processedPath));
    }

    [Fact]
    public void WriteRawAndProcessed_WhenOnlyRawIncluded_WritesOnlyRawFile()
    {
        // Arrange
        using var recorder = CreateRecorder(new AudioRecordingOptions
        {
            Enabled = true,
            IncludeRaw = true,
            IncludeProcessed = false
        });

        // Act
        recorder.WriteRaw(MonoChunk([500], sampleRate: 24000));
        recorder.WriteProcessed(MonoChunk([500], sampleRate: 16000));
        recorder.Dispose();

        // Assert
        Assert.Single(Directory.GetFiles(_baseDirectory, "mic_raw_*.wav"));
        Assert.Empty(Directory.GetFiles(_baseDirectory, "mic_processed_*.wav"));
    }

    [Fact]
    public void WriteAnything_WhenDisabled_WritesNothing()
    {
        // Arrange
        using var recorder = CreateRecorder(new AudioRecordingOptions { Enabled = false });

        // Act
        recorder.WriteRaw(MonoChunk([500], sampleRate: 16000));
        recorder.WriteProcessed(MonoChunk([500], sampleRate: 16000));
        recorder.Dispose();

        // Assert
        Assert.False(Directory.Exists(_baseDirectory));
    }

    [Fact]
    public void Write_MoreThanOneSecond_KeepsFileValidAndAppendsAllData()
    {
        // Arrange — one second at 16 kHz mono triggers the periodic header refresh. The refresh
        // must not lose the write position; data written after it is appended, not overwritten.
        const int samplesPerSecond = 16000;
        var firstSecond = new short[samplesPerSecond];
        var secondSecond = Enumerable.Repeat((short)0x1234, samplesPerSecond).ToArray();

        using var recorder = CreateRecorder(new AudioRecordingOptions { Enabled = true, IncludeRaw = false });

        // Act
        recorder.WriteProcessed(MonoChunk(firstSecond, sampleRate: samplesPerSecond));

        // Assert — header is valid mid-recording (guards against a hard crash)
        var midRecordingBytes = File.ReadAllBytes(Directory.GetFiles(_baseDirectory, "mic_processed_*.wav").Single());
        Assert.Equal(44 + samplesPerSecond * 2, midRecordingBytes.Length);
        Assert.Equal(samplesPerSecond * 2, BinaryPrimitives.ReadInt32LittleEndian(midRecordingBytes.AsSpan(40, 4)));

        // Act (continued)
        recorder.WriteProcessed(MonoChunk(secondSecond, sampleRate: samplesPerSecond));
        recorder.Dispose();

        // Assert
        var bytes = File.ReadAllBytes(Directory.GetFiles(_baseDirectory, "mic_processed_*.wav").Single());
        Assert.Equal(44 + 2 * samplesPerSecond * 2, bytes.Length);
        Assert.Equal(2 * samplesPerSecond * 2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40, 4)));
        Assert.Equal(0x1234, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44 + samplesPerSecond * 2, 2)));
    }

    private AudioRecorder CreateRecorder(AudioRecordingOptions recordingOptions)
    {
        recordingOptions.Directory = _baseDirectory;
        var audioOptions = new AudioOptions { Recording = recordingOptions };
        return new AudioRecorder(Microsoft.Extensions.Options.Options.Create(audioOptions), NullLogger<AudioRecorder>.Instance);
    }

    private static RawPcmChunk MonoChunk(short[] samples, int sampleRate)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        }

        return new RawPcmChunk
        {
            Bytes = bytes,
            Format = new AudioFormat
            {
                Channels = 1,
                SampleRate = sampleRate,
                BitsPerSample = 16
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDirectory))
        {
            Directory.Delete(_baseDirectory, true);
        }
    }
}
