namespace Stefan.Server.Domain;

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

    /// <summary>
    /// JSON array of <see cref="ToolDocumentCommandAction"/> entries describing
    /// which commands created/updated/deleted this document.
    /// </summary>
    public string CommandActions { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }
}
