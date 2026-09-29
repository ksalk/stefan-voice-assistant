namespace Stefan.Server.Domain.ToolEntities;

/// <summary>
/// A tool-scoped document stored in the shared jsonb document table.
/// <see cref="Payload"/> holds the serialized tool entity, <see cref="Type"/>
/// discriminates which tool it belongs to.
/// </summary>
public class ToolDocument
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
