namespace Stefan.Node.Options;

public class AudioInputOptions
{
    public string DeviceName { get; set; } = "plughw:1,0";
    public int SampleRate { get; set; } = 48_000;
    public int Channels { get; set; } = 1;
    public int BitsPerSample { get; set; } = 16;
    public int ProcessingSampleRate { get; set; } = 16_000;
}

public class AudioOutputOptions
{
    public string DeviceName { get; set; } = "default";
}

public class AudioOptions
{
    public const string SectionName = "Audio";
    public string InputSource { get; set; } = "mic";
    public string? PipePath { get; set; }
    public AudioInputOptions Input { get; set; } = new();
    public AudioOutputOptions Output { get; set; } = new();
    public float SilenceThreshold { get; set; } = 0.02f;
    public int SilenceTimeoutMs { get; set; } = 1000;
    public int MaxRecordingMs { get; set; } = 10_000;
}
