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
    Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <param name="filter">Optional predicate applied in memory after deserialization.</param>
    Task<IReadOnlyList<T>> ListAsync<T>(Func<T, bool>? filter = null, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    Task AddAsync<T>(T document, CancellationToken cancellationToken = default) where T : class, IToolDocument;

    /// <summary>Archives the document and removes it from the live table atomically.</summary>
    Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument;
}

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

    public async Task AddAsync<T>(T document, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        dbContext.ToolDocuments.Add(new ToolDocument
        {
            Id = document.Id,
            Type = T.DocumentType,
            Payload = JsonSerializer.Serialize(document),
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IToolDocument
    {
        string documentType = T.DocumentType;
        var document = await dbContext.ToolDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == documentType, cancellationToken);
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

    private static T Deserialize<T>(ToolDocument document) where T : class, IToolDocument =>
        JsonSerializer.Deserialize<T>(document.Payload)
        ?? throw new InvalidOperationException(
            $"Failed to deserialize {typeof(T).Name} document {document.Id}.");
}
