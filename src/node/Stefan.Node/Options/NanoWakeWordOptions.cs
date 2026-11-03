namespace Stefan.Node.Options;

/// <summary>
/// Configuration for the NanoWakeWord wake-word detector. The stop keyword is handled separately
/// by <see cref="KeywordSpotterOptions"/>.
/// </summary>
public class NanoWakeWordOptions
{
    public const string SectionName = "NanoWakeWord";

    /// <summary>Model file name without the <c>.onnx</c> extension, e.g. <c>stefan_v0.2</c>.</summary>
    public string Model { get; set; } = "stefan_v0.2";

    /// <summary>Keyword returned when the model fires. Used in logs and command correlation.</summary>
    public string Keyword { get; set; } = "stefan";

    /// <summary>Minimum model probability for a wake-word candidate.</summary>
    public float Threshold { get; set; } = 0.75f;

    /// <summary>Number of consecutive threshold crossings required before a detection.</summary>
    public int TriggerLevel { get; set; } = 1;

    /// <summary>Cooldown period, in frames, after a detection.</summary>
    public int Refractory { get; set; } = 60;

    /// <summary>Audio frames processed per pipeline step. Four frames is 320 ms at 16 kHz.</summary>
    public int StepFrames { get; set; } = 4;
}
