namespace Stefan.Node.Options;

/// <summary>Selects the wake-word detector implementation used by the node.</summary>
public class WakeWordOptions
{
    public const string SectionName = "WakeWord";
    public const string NanoWakeWordDetector = "nanowakeword";
    public const string SherpaDetector = "sherpa";

    public static readonly IReadOnlyList<string> SupportedDetectors =
        [NanoWakeWordDetector, SherpaDetector];

    /// <summary>Detector to use: <c>nanowakeword</c> (default) or <c>sherpa</c>.</summary>
    public string Detector { get; set; } = NanoWakeWordDetector;

    /// <summary>Returns the canonical detector name or throws when the value is not supported.</summary>
    public static string Normalize(string? detector)
    {
        var normalized = detector?.Trim().ToLowerInvariant();
        return normalized switch
        {
            NanoWakeWordDetector => NanoWakeWordDetector,
            SherpaDetector => SherpaDetector,
            _ => throw new InvalidOperationException(
                $"Unknown wake-word detector '{detector}' in {SectionName}:Detector. " +
                $"Supported values: {string.Join(", ", SupportedDetectors)}.")
        };
    }
}
