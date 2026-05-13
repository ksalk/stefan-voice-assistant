using System.Threading.Channels;
using Alsa.Net;

namespace Stefan.Node.Audio;

public class TestAudioInputProvider : IAudioInputProvider
{
    private const int WavHeaderSize = 44;
    private const int ChunkSize = 4096;
    private const int ExpectedSampleRate = 16000;
    private const int ExpectedChannels = 2;
    private const int ExpectedBitsPerSample = 16;

    public async Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        await WriteSilenceFor(TimeSpan.FromSeconds(3), audioWriter, cancellationToken);

        var writeResult = await WriteFileContent("test_audio.wav", audioWriter, cancellationToken);
        if (!writeResult)
        {
            audioWriter.Complete();
            return;
        }

        // Write silence to keep the pipeline alive until cancelled
        await WriteSilence(audioWriter, cancellationToken);

        audioWriter.Complete();
    }

    private async Task WriteSilence(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        var silenceChunk = new byte[ChunkSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            await audioWriter.WriteAsync(silenceChunk, cancellationToken);
            await Task.Delay(64, cancellationToken); // ~64ms per 4096-byte chunk at 16kHz stereo 16-bit
        }
    }

    private async Task WriteSilenceFor(TimeSpan duration, ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        var silenceChunk = new byte[ChunkSize];
        var endTime = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            await audioWriter.WriteAsync(silenceChunk, cancellationToken);
            await Task.Delay(64, cancellationToken); // ~64ms per 4096-byte chunk at 16kHz stereo 16-bit
        }
    }

    private async Task<bool> WriteFileContent(string wavFilePath, ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        if (!File.Exists(wavFilePath))
        {
            Console.WriteLine($"Test audio file not found: {wavFilePath}");
            return false;
        }

        var wavBytes = await File.ReadAllBytesAsync(wavFilePath, cancellationToken);

        if (wavBytes.Length < WavHeaderSize)
        {
            Console.WriteLine("Invalid WAV file: too small to contain a header");
            return false;
        }

        ValidateWavHeader(wavBytes);

        var pcmData = wavBytes[WavHeaderSize..];
        var offset = 0;

        while (offset < pcmData.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var size = Math.Min(ChunkSize, pcmData.Length - offset);
            var chunk = pcmData[offset..(offset + size)];
            await audioWriter.WriteAsync(chunk, cancellationToken);

            offset += size;
        }

        return true;
    }

    private static void ValidateWavHeader(byte[] wavBytes)
    {
        // RIFF header: "RIFF" at 0-3, file size at 4-7, "WAVE" at 8-11
        var riff = System.Text.Encoding.ASCII.GetString(wavBytes, 0, 4);
        var wave = System.Text.Encoding.ASCII.GetString(wavBytes, 8, 4);

        if (riff != "RIFF" || wave != "WAVE")
        {
            Console.WriteLine("Warning: File does not appear to be a valid WAV file (missing RIFF/WAVE signatures)");
            return;
        }

        // fmt subchunk: audio format at 20-21, channels at 22-23, sample rate at 24-27, bits per sample at 34-35
        var audioFormat = BitConverter.ToUInt16(wavBytes, 20);
        var channels = BitConverter.ToUInt16(wavBytes, 22);
        var sampleRate = BitConverter.ToInt32(wavBytes, 24);
        var bitsPerSample = BitConverter.ToUInt16(wavBytes, 34);

        if (audioFormat != 1)
            Console.WriteLine($"Warning: Expected PCM format (1), got {audioFormat}");

        if (channels != ExpectedChannels)
            Console.WriteLine($"Warning: Expected {ExpectedChannels} channels, got {channels}");

        if (sampleRate != ExpectedSampleRate)
            Console.WriteLine($"Warning: Expected sample rate {ExpectedSampleRate}, got {sampleRate}");

        if (bitsPerSample != ExpectedBitsPerSample)
            Console.WriteLine($"Warning: Expected {ExpectedBitsPerSample} bits per sample, got {bitsPerSample}");
    }
}