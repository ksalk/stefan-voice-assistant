namespace Stefan.Server.Domain;

/// <summary>
/// Archive copy of a <see cref="ToolDocument"/> created when the live
/// document is deleted. Retains the original payload unchanged plus
/// <see cref="ArchivedAt"/>.
/// </summary>
/// <remarks>
/// A document can be deleted more than once, so the archive keeps one row
/// per delete event. <see cref="ArchiveId"/> identifies the archive row,
/// while <see cref="Id"/> stays the id of the archived document.
/// </remarks>
public class ToolDocumentArchive
{
    /// <summary>Primary key of the archive row.</summary>
    public Guid ArchiveId { get; set; }

    /// <summary>Id of the archived document. The same document can be archived many times.</summary>
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
