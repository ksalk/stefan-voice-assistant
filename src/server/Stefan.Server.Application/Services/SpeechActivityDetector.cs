using System.Buffers.Binary;
using Stefan.Server.Application.Options;

namespace Stefan.Server.Application.Services;

public readonly record struct SpeechAnalysis(
    bool HasSpeech,
    double SpeechDurationMs,
    double? MaxFrameDbfs);

/// <summary>
/// Cheap energy-based speech check for command audio. It only separates "nobody spoke"
/// from "possibly somebody spoke"; it is not a full VAD. Unsupported or malformed audio
/// is treated as speech so the command still reaches STT.
/// </summary>
public static class SpeechActivityDetector
{
    private const int PcmFormat = 1;
    private const int MinimumWavHeaderLength = 12;

    public static SpeechAnalysis Analyze(byte[] wavBytes, SpeechDetectionOptions options)
    {
        if (!options.Enabled || wavBytes is null || wavBytes.Length < MinimumWavHeaderLength)
        {
            return FailOpen();
        }

        if (!HasTag(wavBytes, 0, "RIFF"u8) || !HasTag(wavBytes, 8, "WAVE"u8))
        {
            return FailOpen();
        }

        FormatInfo? format = null;
        var dataOffset = -1;
        var dataSize = 0;

        var offset = 12;
        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = wavBytes.AsSpan(offset, 4);
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.AsSpan(offset + 4, 4));
            if (chunkSize < 0)
            {
                return FailOpen();
            }

            var chunkDataOffset = offset + 8;
            var available = wavBytes.Length - chunkDataOffset;
            var effectiveSize = Math.Min(chunkSize, available);

            if (chunkId.SequenceEqual("fmt "u8))
            {
                if (effectiveSize < 16)
                {
                    return FailOpen();
                }

                var fmt = wavBytes.AsSpan(chunkDataOffset, effectiveSize);
                format = new FormatInfo(
                    BinaryPrimitives.ReadUInt16LittleEndian(fmt),
                    BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]),
                    BinaryPrimitives.ReadInt32LittleEndian(fmt[4..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]));
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                dataOffset = chunkDataOffset;
                dataSize = effectiveSize;
            }

            var next = (long)chunkDataOffset + chunkSize + (chunkSize % 2);
            if (next <= offset || next > int.MaxValue)
            {
                break;
            }

            offset = (int)next;
        }

        if (format is not { } formatInfo || dataOffset < 0)
        {
            return FailOpen();
        }

        if (formatInfo.AudioFormat != PcmFormat ||
            formatInfo.BitsPerSample != 16 ||
            formatInfo.Channels == 0 ||
            formatInfo.SampleRate <= 0)
        {
            return FailOpen();
        }

        return AnalyzePcm16(wavBytes.AsSpan(dataOffset, dataSize), formatInfo, options);
    }

    private static SpeechAnalysis AnalyzePcm16(
        ReadOnlySpan<byte> data,
        FormatInfo format,
        SpeechDetectionOptions options)
    {
        var frameMs = options.FrameMs > 0 ? options.FrameMs : 30;
        var samplesPerFrame = (long)format.SampleRate * frameMs / 1000;
        var frameBytesLong = samplesPerFrame * format.Channels * sizeof(short);
        if (frameBytesLong <= 0 || frameBytesLong > int.MaxValue)
        {
            return FailOpen();
        }

        var frameBytes = (int)frameBytesLong;
        var totalFrames = data.Length / frameBytes;
        if (totalFrames == 0)
        {
            return new SpeechAnalysis(false, 0, null);
        }

        var minSpeechMs = Math.Max(1, options.MinSpeechMs);
        var framesAboveThreshold = 0;
        var maxFrameDbfs = double.NegativeInfinity;

        for (var frame = 0; frame < totalFrames; frame++)
        {
            var frameBytesSpan = data.Slice(frame * frameBytes, frameBytes);
            var sampleCount = frameBytesSpan.Length / sizeof(short);

            double sumSquares = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(frameBytesSpan.Slice(i * sizeof(short), sizeof(short)));
                sumSquares += (double)sample * sample;
            }

            var rms = Math.Sqrt(sumSquares / sampleCount) / short.MaxValue;
            var dbfs = 20 * Math.Log10(Math.Max(rms, 1e-9));
            maxFrameDbfs = Math.Max(maxFrameDbfs, dbfs);

            if (dbfs >= options.ThresholdDbfs)
            {
                framesAboveThreshold++;
            }
        }

        var speechDurationMs = framesAboveThreshold * (double)frameMs;
        return new SpeechAnalysis(
            speechDurationMs >= minSpeechMs,
            speechDurationMs,
            double.IsNegativeInfinity(maxFrameDbfs) ? null : maxFrameDbfs);
    }

    private static SpeechAnalysis FailOpen() => new(true, 0, null);

    private static bool HasTag(byte[] data, int offset, ReadOnlySpan<byte> tag) =>
        data.Length >= offset + tag.Length &&
        data.AsSpan(offset, tag.Length).SequenceEqual(tag);

    private readonly record struct FormatInfo(
        ushort AudioFormat,
        ushort Channels,
        int SampleRate,
        ushort BitsPerSample);
}
