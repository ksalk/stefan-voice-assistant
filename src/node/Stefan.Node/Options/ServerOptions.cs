namespace Stefan.Node.Options;

public class ServerOptions
{
    public const string SectionName = "Server";
    public string Url { get; set; } = "http://0.0.0.0:8080";
}