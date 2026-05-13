namespace Stefan.Node.Options;

public class RemoteServerOptions
{
    public const string SectionName = "RemoteServer";
    public string Url { get; set; } = string.Empty;
    public string AuthSecret { get; set; } = string.Empty;
}
