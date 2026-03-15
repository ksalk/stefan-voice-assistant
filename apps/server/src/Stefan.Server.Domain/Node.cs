namespace Stefan.Server.Domain;

public class Node
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Hostname { get; set; }
    public required string Port { get; set; }
}
