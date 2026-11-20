namespace Stefan.Server.Application.Options;

/// <summary>
/// Energy-based speech detection for command audio. The defaults come from production
/// false wake-word clips: those clips had at most ~120 ms above -35 dBFS, while real
/// commands had at least ~570 ms.
/// </summary>
public class SpeechDetectionOptions
{
    public const string SectionName = "SpeechDetection";

    /// <summary>When false, the server always runs STT and relies on the empty-transcript fallback.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Analysis frame length in milliseconds.</summary>
    public int FrameMs { get; set; } = 30;

    /// <summary>Frames at or above this level count as speech. -35 dBFS is about 0.018 RMS.</summary>
    public double ThresholdDbfs { get; set; } = -35;

    /// <summary>Total speech energy required before the command audio is sent to STT.</summary>
    public int MinSpeechMs { get; set; } = 200;
}
