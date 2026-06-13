using System.Buffers.Binary;

namespace Stefan.Node.Audio;

/// <summary>
/// Provides low-level audio processing helpers for 16-bit PCM data.
/// </summary>
/// <remarks>
/// <para>Audio terms used in this class:</para>
/// <list type="bullet">
///   <item>
///     <term>PCM</term>
///     <description>
///       Pulse Code Modulation — the simplest digital representation of sound.
///       It stores audio as a sequence of numbers (samples), where each number
///       represents the air pressure at a single point in time.
///     </description>
///   </item>
///   <item>
///     <term>Sample</term>
///     <description>
///       One numeric value that captures the sound wave at one instant.
///       In this code each sample is a 16-bit signed integer (<see cref="short"/>).
///     </description>
///   </item>
///   <item>
///     <term>Sample rate</term>
///     <description>
///       How many samples are recorded every second, measured in Hertz (Hz).
///       For example, 16000 Hz means 16000 samples per second.
///     </description>
///   </item>
///   <item>
///     <term>Bits per sample</term>
///     <description>
///       How many bits are used to store one sample. This code only supports 16-bit PCM,
///       which means each sample is stored as two bytes.
///     </description>
///   </item>
///   <item>
///     <term>Channel</term>
///     <description>
///       A separate audio stream. Mono = 1 channel, stereo = 2 channels (left and right).
///     </description>
///   </item>
///   <item>
///     <term>Frame</term>
///     <description>
///       One sample for every channel at the same point in time.
///       For stereo, one frame contains two samples (left + right).
///     </description>
///   </item>
///   <item>
///     <term>Interleaved</term>
///     <description>
///       How multi-channel PCM bytes are arranged: the samples for each channel
///       alternate, e.g. left 0, right 0, left 1, right 1, ...
///     </description>
///   </item>
///   <item>
///     <term>Mono</term>
///     <description>
///       Audio with a single channel. This code converts multi-channel input to mono
///       by averaging the samples of every frame.
///     </description>
///   </item>
///   <item>
///     <term>Resampling</term>
///     <description>
///       Changing the sample rate of an audio stream, for example converting 48000 Hz
///       audio to 16000 Hz. This implementation uses linear interpolation to estimate
///       sample values between the original samples.
///     </description>
///   </item>
///   <item>
///     <term>Little-endian</term>
///     <description>
///       The byte order used to store each 16-bit sample: the least-significant byte
///       comes first. This is the standard format for WAV PCM data on most systems.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class AudioProcessing
{
    /// <summary>
    /// Converts a chunk of multi-channel 16-bit PCM data into single-channel (mono) samples.
    /// For mono input the bytes are simply reinterpreted as samples without any mixing.
    /// For multi-channel input the samples of each frame are averaged to produce one mono sample.
    /// </summary>
    /// <param name="input">The raw PCM chunk to convert.</param>
    /// <returns>The mono samples at the same sample rate as the input.</returns>
    /// <exception cref="NotSupportedException">The PCM format is not 16-bit.</exception>
    /// <exception cref="ArgumentException">The byte buffer size does not match the declared format.</exception>
    public static MonoPcmSamples ConvertToMono(RawPcmChunk input)
    {
        var format = input.Format;
        var audioBytes = input.Bytes;

        if (format.BitsPerSample != 16)
        {
            throw new NotSupportedException($"Only 16-bit PCM is supported. Got {format.BitsPerSample} bits per sample.");
        }

        var bytesPerSample = format.BitsPerSample / 8;
        var frameSize = bytesPerSample * format.Channels;

        if (audioBytes.Length % frameSize != 0)
        {
            throw new ArgumentException(
                $"The byte buffer length ({audioBytes.Length}) is not aligned to the frame size ({frameSize}) for {format.Channels} channel(s) at {format.BitsPerSample} bits per sample.",
                nameof(input));
        }

        if (format.Channels == 1)
        {
            var sampleCount = audioBytes.Length / bytesPerSample;
            var mono = new short[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                mono[i] = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.AsSpan(i * bytesPerSample, bytesPerSample));
            }

            return new MonoPcmSamples { Samples = mono, SampleRate = format.SampleRate };
        }

        var frameCount = audioBytes.Length / frameSize;
        var monoSamples = new short[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0;
            for (var ch = 0; ch < format.Channels; ch++)
            {
                var offset = (frame * format.Channels + ch) * bytesPerSample;
                sum += BinaryPrimitives.ReadInt16LittleEndian(audioBytes.AsSpan(offset, bytesPerSample));
            }
            monoSamples[frame] = (short)(sum / format.Channels);
        }

        return new MonoPcmSamples { Samples = monoSamples, SampleRate = format.SampleRate };
    }

    /// <summary>
    /// Changes the sample rate of a mono 16-bit sample stream using linear interpolation.
    /// If the input and target sample rates are equal, the samples are returned unchanged.
    /// </summary>
    /// <param name="input">The mono samples to resample.</param>
    /// <param name="targetSampleRate">The desired output sample rate in Hertz.</param>
    /// <returns>The resampled mono samples.</returns>
    /// <exception cref="ArgumentException">The target sample rate is not positive.</exception>
    public static MonoPcmSamples Resample(MonoPcmSamples input, int targetSampleRate)
    {
        if (targetSampleRate <= 0)
        {
            throw new ArgumentException("Target sample rate must be greater than zero.", nameof(targetSampleRate));
        }

        if (input.SampleRate == targetSampleRate)
        {
            return input;
        }

        var sourceSamples = input.Samples;
        var ratio = (double)input.SampleRate / targetSampleRate;
        var outputLength = (int)(sourceSamples.Length / ratio);
        var output = new short[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var srcPos = i * ratio;
            var srcIndex = (int)srcPos;
            var frac = srcPos - srcIndex;

            if (srcIndex + 1 < sourceSamples.Length)
            {
                var interpolated = sourceSamples[srcIndex] * (1.0 - frac) + sourceSamples[srcIndex + 1] * frac;
                output[i] = (short)Math.Clamp(interpolated, short.MinValue, short.MaxValue);
            }
            else
            {
                output[i] = sourceSamples[srcIndex];
            }
        }

        return new MonoPcmSamples { Samples = output, SampleRate = targetSampleRate };
    }

    /// <summary>
    /// Converts mono 16-bit samples back into a raw 16-bit little-endian PCM byte chunk.
    /// </summary>
    /// <param name="input">The mono samples to convert.</param>
    /// <returns>A raw PCM chunk containing the bytes and the matching mono 16-bit format.</returns>
    public static RawPcmChunk ConvertSamplesToBytes(MonoPcmSamples input)
    {
        var samples = input.Samples;
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
                SampleRate = input.SampleRate,
                BitsPerSample = 16
            }
        };
    }

    /// <summary>
    /// Convenience pipeline: converts a raw PCM chunk to mono, resamples it to the target rate,
    /// and returns the resulting raw PCM bytes.
    /// </summary>
    /// <param name="input">The raw PCM chunk to process.</param>
    /// <param name="targetSampleRate">The desired output sample rate in Hertz.</param>
    /// <returns>A raw PCM chunk containing mono 16-bit samples at the target sample rate.</returns>
    public static RawPcmChunk ConvertAndResample(RawPcmChunk input, int targetSampleRate)
    {
        var monoPcm = ConvertToMono(input);
        var resampled = Resample(monoPcm, targetSampleRate);
        return ConvertSamplesToBytes(resampled);
    }
}
