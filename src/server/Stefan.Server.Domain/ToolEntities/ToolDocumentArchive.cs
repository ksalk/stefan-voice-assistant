namespace Stefan.Server.Domain.ToolEntities;

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
    public DateTime CreatedAt { get; set; }
    public DateTime ArchivedAt { get; set; }
}
