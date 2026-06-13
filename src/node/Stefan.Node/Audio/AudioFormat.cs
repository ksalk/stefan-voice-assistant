namespace Stefan.Node.Audio;

/// <summary>
/// Describes the digital format of a PCM audio stream.
/// </summary>
public readonly record struct AudioFormat
{
    /// <summary>
    /// Number of audio channels, e.g. 1 for mono, 2 for stereo.
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Sample rate in Hertz (samples per second), e.g. 16000.
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Bits used to store a single sample, e.g. 16 for 16-bit PCM.
    /// </summary>
    public int BitsPerSample { get; init; }
}
