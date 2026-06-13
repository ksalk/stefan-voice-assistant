namespace Stefan.Node.Audio;

/// <summary>
/// A chunk of raw, interleaved PCM audio bytes.
/// For 16-bit PCM the bytes are stored in little-endian order and the channels are interleaved:
/// sample 0 channel 0, sample 0 channel 1, sample 1 channel 0, sample 1 channel 1, ...
/// </summary>
public readonly record struct RawPcmChunk
{
    /// <summary>
    /// The raw PCM bytes.
    /// </summary>
    public byte[] Bytes { get; init; }

    /// <summary>
    /// The format of the PCM data contained in <see cref="Bytes"/>.
    /// </summary>
    public AudioFormat Format { get; init; }
}
