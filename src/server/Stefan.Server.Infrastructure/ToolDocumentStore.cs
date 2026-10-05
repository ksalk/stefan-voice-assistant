using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Infrastructure;

/// <summary>
/// Typed access to tool-scoped documents stored in the shared
/// tools."ToolDocuments" jsonb table. Deleting a document moves it
/// to tools."ToolDocumentArchive" in the same transaction.
/// Mutation methods track which command performed the change
/// (see <see cref="ToolDocument.CommandActions"/>); pass a null
/// commandId for non-command callers (e.g. Quartz jobs), which
/// records nothing.
/// </summary>
public interface IToolDocumentStore
{
    Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <param name="filter">Optional predicate applied in memory after deserialization.</param>
    Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <summary>Inserts the document. When <paramref name="commandId"/> is set, a "created" entry is recorded.</summary>
    Task AddAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <summary>Overwrites the stored payload. When <paramref name="commandId"/> is set, an "updated" entry is recorded.</summary>
    Task UpdateAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <summary>
    /// Archives the document and removes it from the live table atomically.
    /// When <paramref name="commandId"/> is set, a "deleted" entry is recorded (and archived along with the document).
    /// </summary>
    Task DeleteAsync<T>(Guid id, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <summary>Lightweight references to all documents (live + archived) touched by the given command.</summary>
    Task<IReadOnlyList<ToolDocumentCommandRef>> ListByCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    /// <summary>Documents of the given type (live + archived) touched by the given command, deserialized.</summary>
    Task<IReadOnlyList<T>> ListByCommandAsync<T>(Guid commandId, CancellationToken cancellationToken = default) where T : class, IToolDocument;
}

/// <summary>Lightweight reference to a tool document touched by a command, across all document types.</summary>
public record ToolDocumentCommandRef(Guid Id, string Type, DateTime CreatedAt);

public class ToolDocumentStore(StefanDbContext dbContext) : IToolDocumentStore
{
    public async Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;
        var document = await dbContext.ToolDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == documentType, cancellationToken);

        return document == null ? null : Deserialize<T>(document);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;
        var documents = await dbContext.ToolDocuments
            .AsNoTracking()
            .Where(d => d.Type == documentType)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        IEnumerable<T> result = documents.Select(Deserialize<T>);
        if (filter != null)
            result = result.Where(filter);

        return result.ToList();
    }

    public async Task AddAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        dbContext.ToolDocuments.Add(new ToolDocument
        {
            Id = document.Id,
            Type = T.DocumentType,
            Payload = JsonSerializer.Serialize(document),
            CommandActions = Append(null, commandId, ToolDocumentCommandActionType.Created),
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;
        var live = await dbContext.ToolDocuments
            .FirstOrDefaultAsync(d => d.Id == document.Id && d.Type == documentType, cancellationToken);
        if (live == null)
            return;

        live.Payload = JsonSerializer.Serialize(document);
        live.CommandActions = Append(live.CommandActions, commandId, ToolDocumentCommandActionType.Updated);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync<T>(Guid id, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;
        var document = await dbContext.ToolDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == documentType, cancellationToken);
        if (document == null)
            return;

        // The "deleted" entry is written to the archive copy directly, so the
        // archive insert and the live-row removal stay in one transaction.
        dbContext.ToolDocumentArchive.Add(new ToolDocumentArchive
        {
            Id = document.Id,
            Type = document.Type,
            Payload = document.Payload,
            CommandActions = Append(document.CommandActions, commandId, ToolDocumentCommandActionType.Deleted),
            CreatedAt = document.CreatedAt,
            ArchivedAt = DateTime.UtcNow,
        });
        dbContext.ToolDocuments.Remove(document);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ToolDocumentCommandRef>> ListByCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        var liveRefs = await QueryByCommand(dbContext.ToolDocuments, commandId)
            .Select(d => new { d.Id, d.Type, d.CreatedAt })
            .ToListAsync(cancellationToken);
        var archivedRefs = await QueryByCommand(dbContext.ToolDocumentArchive, commandId)
            .Select(d => new { d.Id, d.Type, d.CreatedAt })
            .ToListAsync(cancellationToken);

        return liveRefs.Select(d => new ToolDocumentCommandRef(d.Id, d.Type, d.CreatedAt))
            .Concat(archivedRefs.Select(d => new ToolDocumentCommandRef(d.Id, d.Type, d.CreatedAt)))
            .ToList();
    }

    public async Task<IReadOnlyList<T>> ListByCommandAsync<T>(Guid commandId, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;

        var live = await QueryByCommand(dbContext.ToolDocuments, commandId)
            .Where(d => d.Type == documentType)
            .ToListAsync(cancellationToken);
        var archived = await QueryByCommand(dbContext.ToolDocumentArchive, commandId)
            .Select(d => new ToolDocument { Id = d.Id, Type = d.Type, Payload = d.Payload, CommandActions = d.CommandActions, CreatedAt = d.CreatedAt })
            .ToListAsync(cancellationToken);

        var archivedRefs = archived
            .Where(d => d.Type == documentType)
            .Select(Deserialize<T>);

        return live.Select(Deserialize<T>).Concat(archivedRefs).ToList();
    }

    // CommandActions is an array; containment must use the array form:
    // CommandActions @> '[{"CommandId":"..."}]' matches entries with that CommandId.
    private IQueryable<ToolDocument> QueryByCommand(IQueryable<ToolDocument> documents, Guid commandId) =>
        documents.Where(d => EF.Functions.JsonContains(
            d.CommandActions,
            JsonSerializer.Serialize(new[] { new { CommandId = commandId } })));

    private IQueryable<ToolDocumentArchive> QueryByCommand(IQueryable<ToolDocumentArchive> documents, Guid commandId) =>
        documents.Where(d => EF.Functions.JsonContains(
            d.CommandActions,
            JsonSerializer.Serialize(new[] { new { CommandId = commandId } })));

    private IQueryable<ToolDocumentCommandRef> QueryByCommandAsRef(IQueryable<ToolDocument> documents, Guid commandId) =>
        QueryByCommand(documents, commandId)
            .Select(d => new { d.Id, d.Type, d.CreatedAt })
            .AsEnumerable()
            .Select(d => new ToolDocumentCommandRef(d.Id, d.Type, d.CreatedAt))
            .AsQueryable();

    private static string Append(string? commandActions, Guid? commandId, string action)
    {
        if (commandId == null)
            return commandActions ?? "[]";

        var entries = string.IsNullOrEmpty(commandActions)
            ? new List<ToolDocumentCommandAction>()
            : JsonSerializer.Deserialize<List<ToolDocumentCommandAction>>(commandActions) ?? [];

        entries.Add(new ToolDocumentCommandAction(commandId.Value, action, DateTime.UtcNow));
        return JsonSerializer.Serialize(entries);
    }

    private static T Deserialize<T>(ToolDocument document) where T : class, IToolDocument =>
        JsonSerializer.Deserialize<T>(document.Payload)
        ?? throw new InvalidOperationException(
            $"Failed to deserialize {typeof(T).Name} document {document.Id}.");
}
