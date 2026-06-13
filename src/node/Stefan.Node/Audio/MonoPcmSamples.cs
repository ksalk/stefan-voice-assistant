namespace Stefan.Node.Audio;

/// <summary>
/// A sequence of single-channel (mono) 16-bit audio samples at a specific sample rate.
/// </summary>
public readonly record struct MonoPcmSamples
{
    /// <summary>
    /// The mono samples as 16-bit signed integers.
    /// </summary>
    public short[] Samples { get; init; }

    /// <summary>
    /// Sample rate of the samples in Hertz.
    /// </summary>
    public int SampleRate { get; init; }
}
