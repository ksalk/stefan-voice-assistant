namespace Stefan.Server.Domain;

/// <summary>
/// Archive copy of a <see cref="ToolDocument"/> created when the live
/// document is deleted. Retains the original payload unchanged plus
/// <see cref="ArchivedAt"/>.
/// </summary>
public class ToolDocumentArchive
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;

    /// <summary>
    /// Command actions copied from the live <see cref="ToolDocument"/> at delete time.
    /// </summary>
    public string CommandActions { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }
    public DateTime ArchivedAt { get; set; }
}
