using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Infrastructure;

/// <summary>
/// Typed access to tool-scoped documents stored in the shared
/// tools."ToolDocuments" jsonb table. Deleting a document moves it
/// to tools."ToolDocumentArchive" in the same transaction.
/// </summary>
public interface IToolDocumentStore
{
    Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class;

    /// <param name="filter">Optional predicate applied in memory after deserialization.</param>
    Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class;

    Task AddAsync<T>(Guid id, T document, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Archives the document and removes it from the live table atomically.</summary>
    Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class;
}

public class ToolDocumentStore(StefanDbContext dbContext) : IToolDocumentStore
{
    public async Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class
    {
        var document = await dbContext.ToolDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == GetDocumentType<T>(), cancellationToken);

        return document == null ? null : Deserialize<T>(document);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class
    {
        var documents = await dbContext.ToolDocuments
            .AsNoTracking()
            .Where(d => d.Type == GetDocumentType<T>())
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        IEnumerable<T> result = documents.Select(Deserialize<T>);
        if (filter != null)
            result = result.Where(filter);

        return result.ToList();
    }

    public async Task AddAsync<T>(Guid id, T document, CancellationToken cancellationToken = default) where T : class
    {
        dbContext.ToolDocuments.Add(new ToolDocument
        {
            Id = id,
            Type = GetDocumentType<T>(),
            Payload = JsonSerializer.Serialize(document),
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class
    {
        var document = await dbContext.ToolDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == GetDocumentType<T>(), cancellationToken);
        if (document == null)
            return;

        dbContext.ToolDocumentArchive.Add(new ToolDocumentArchive
        {
            Id = document.Id,
            Type = document.Type,
            Payload = document.Payload,
            CreatedAt = document.CreatedAt,
            ArchivedAt = DateTime.UtcNow,
        });
        dbContext.ToolDocuments.Remove(document);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetDocumentType<T>() where T : class =>
        typeof(T).GetCustomAttribute<ToolDocumentTypeAttribute>(inherit: false)?.Type
        ?? throw new InvalidOperationException(
            $"{typeof(T).Name} does not declare a {nameof(ToolDocumentTypeAttribute)}.");

    private static T Deserialize<T>(ToolDocument document) where T : class =>
        JsonSerializer.Deserialize<T>(document.Payload)
        ?? throw new InvalidOperationException(
            $"Failed to deserialize {typeof(T).Name} document {document.Id}.");
}
