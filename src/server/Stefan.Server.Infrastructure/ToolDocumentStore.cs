using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// <remarks>
/// Every operation runs in its own scope and therefore in its own
/// <see cref="StefanDbContext"/>. Tool writes never flush the pending
/// changes of the caller, and a failed save leaves no broken change
/// tracker behind.
/// </remarks>
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

    /// <summary>Raw documents of all types (live + archived) touched by the given command.</summary>
    Task<IReadOnlyList<ToolDocument>> ListDocumentsByCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    /// <summary>Archived documents touched by the given command.</summary>
    Task<IReadOnlyList<ToolDocumentArchive>> ListArchiveByCommandAsync(Guid commandId, CancellationToken cancellationToken = default);
}

public class ToolDocumentStore(IServiceScopeFactory scopeFactory) : IToolDocumentStore
{
    public Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument
        => QueryAsync<T?>(async dbContext =>
        {
            string documentType = T.DocumentType;
            var document = await dbContext.ToolDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id && d.Type == documentType, cancellationToken);

            return document == null ? null : Deserialize<T>(document);
        });

    public Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
        => QueryAsync<IReadOnlyList<T>>(async dbContext =>
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
        });

    public Task AddAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
        => ExecuteAsync(async dbContext =>
        {
            dbContext.ToolDocuments.Add(new ToolDocument
            {
                Id = document.Id,
                Type = T.DocumentType,
                Payload = JsonSerializer.Serialize(document),
                CommandActions = Append(null, commandId, ToolDocumentCommandActionType.Created),
                CreatedAt = DateTime.UtcNow,
            });

            await SaveAsync(dbContext, cancellationToken);
        });

    public Task UpdateAsync<T>(T document, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
        => ExecuteAsync(async dbContext =>
        {
            string documentType = T.DocumentType;
            var live = await dbContext.ToolDocuments
                .FirstOrDefaultAsync(d => d.Id == document.Id && d.Type == documentType, cancellationToken);
            if (live == null)
                return;

            live.Payload = JsonSerializer.Serialize(document);
            live.CommandActions = Append(live.CommandActions, commandId, ToolDocumentCommandActionType.Updated);

            await SaveAsync(dbContext, cancellationToken);
        });

    public Task DeleteAsync<T>(Guid id, Guid? commandId = null, CancellationToken cancellationToken = default) where T : class, IToolDocument
        => ExecuteAsync(async dbContext =>
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
                ArchiveId = Guid.NewGuid(),
                Id = document.Id,
                Type = document.Type,
                Payload = document.Payload,
                CommandActions = Append(document.CommandActions, commandId, ToolDocumentCommandActionType.Deleted),
                CreatedAt = document.CreatedAt,
                ArchivedAt = DateTime.UtcNow,
            });
            dbContext.ToolDocuments.Remove(document);

            await SaveAsync(dbContext, cancellationToken);
        });

    public Task<IReadOnlyList<ToolDocument>> ListDocumentsByCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
        => QueryAsync<IReadOnlyList<ToolDocument>>(async dbContext =>
        {
            var live = await QueryByCommand(dbContext.ToolDocuments, commandId)
                .ToListAsync(cancellationToken);
            var archived = await QueryByCommand(dbContext.ToolDocumentArchive, commandId)
                .Select(d => new ToolDocument { Id = d.Id, Type = d.Type, Payload = d.Payload, CommandActions = d.CommandActions, CreatedAt = d.CreatedAt })
                .ToListAsync(cancellationToken);

            return live.Concat(archived).ToList();
        });

    public Task<IReadOnlyList<ToolDocumentArchive>> ListArchiveByCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
        => QueryAsync<IReadOnlyList<ToolDocumentArchive>>(async dbContext =>
        {
            var archived = await QueryByCommand(dbContext.ToolDocumentArchive, commandId)
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<ToolDocumentArchive>)archived;
        });

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

    /// <summary>Runs a read in its own scope, with its own database context.</summary>
    private async Task<TResult> QueryAsync<TResult>(Func<StefanDbContext, Task<TResult>> query)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StefanDbContext>();
        return await query(dbContext);
    }

    /// <summary>Runs a write in its own scope, with its own database context.</summary>
    private async Task ExecuteAsync(Func<StefanDbContext, Task> action)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StefanDbContext>();
        await action(dbContext);
    }

    /// <summary>
    /// Saves the changes of the operation. A failed save leaves the change
    /// tracker with entries that no longer match the database, so the tracker
    /// is cleared before the error travels on.
    /// </summary>
    private static async Task SaveAsync(StefanDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
