namespace Stefan.Node.Audio;

public interface IWakeWordDetector : IDisposable
{
    /// <summary>Processes one chunk of mono 16-bit PCM audio and returns the detected keyword, if any.</summary>
    string? Process(RawPcmChunk chunk);

    /// <summary>Clears any audio and decoding state held by the detector.</summary>
    void Reset();
}
