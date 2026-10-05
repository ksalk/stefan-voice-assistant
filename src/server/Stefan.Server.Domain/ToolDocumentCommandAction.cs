namespace Stefan.Server.Domain;

public static class ToolDocumentCommandActionType
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
}

/// <summary>
/// A single command-driven change to a tool document, stored inside
/// <see cref="ToolDocument.CommandActions"/> / <see cref="ToolDocumentArchive.CommandActions"/>.
/// </summary>
public record ToolDocumentCommandAction(Guid CommandId, string Action, DateTime AtUtc);
