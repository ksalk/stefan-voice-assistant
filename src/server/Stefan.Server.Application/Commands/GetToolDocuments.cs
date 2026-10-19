using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class GetToolDocumentsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Scope { get; set; } = "live";
    public string? Type { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}

public class ToolDocumentDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
    public JsonElement Payload { get; set; }
}

public class GetToolDocumentsResult
{
    public List<ToolDocumentDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetToolDocuments(StefanDbContext dbContext)
{
    private const int MaxPageSize = 100;

    private class Row
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string Payload { get; set; } = null!;
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
    }

    public async Task<GetToolDocumentsResult> Handle(GetToolDocumentsRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Min(MaxPageSize, Math.Max(1, request.PageSize));

        var scope = (request.Scope ?? "live").Trim().ToLowerInvariant();
        var sortBy = (request.SortBy ?? "createdAt").Trim().ToLowerInvariant();
        var descending = !string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var typeFilter = string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim();

        Guid? searchGuid = null;
        if (!string.IsNullOrWhiteSpace(request.Search) && Guid.TryParse(request.Search.Trim(), out var parsedGuid))
        {
            searchGuid = parsedGuid;
        }

        var rawSearch = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var escapedSearch = rawSearch?.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var term = escapedSearch == null ? null : $"%{escapedSearch}%";

        IQueryable<ToolDocument> liveEntities = dbContext.ToolDocuments.AsNoTracking();
        if (typeFilter != null)
        {
            liveEntities = liveEntities.Where(d => d.Type == typeFilter);
        }

        if (term != null)
        {
            var capturedTerm = term;
            var capturedGuid = searchGuid;
            liveEntities = liveEntities.Where(d =>
                (capturedGuid.HasValue && d.Id == capturedGuid.Value) ||
                EF.Functions.ILike(d.Type, capturedTerm, "\\") ||
                EF.Functions.ILike(Convert.ToString(d.Payload), capturedTerm, "\\"));
        }

        var liveQuery = liveEntities.Select(d => new Row
        {
            Id = d.Id,
            Type = d.Type,
            CreatedAt = d.CreatedAt,
            Payload = d.Payload,
            IsArchived = false,
            ArchivedAt = null
        });

        IQueryable<ToolDocumentArchive> archiveEntities = dbContext.ToolDocumentArchive.AsNoTracking();
        if (typeFilter != null)
        {
            archiveEntities = archiveEntities.Where(a => a.Type == typeFilter);
        }

        if (term != null)
        {
            var capturedTerm = term;
            var capturedGuid = searchGuid;
            archiveEntities = archiveEntities.Where(a =>
                (capturedGuid.HasValue && a.Id == capturedGuid.Value) ||
                EF.Functions.ILike(a.Type, capturedTerm, "\\") ||
                EF.Functions.ILike(Convert.ToString(a.Payload), capturedTerm, "\\"));
        }

        var archiveQuery = archiveEntities.Select(a => new Row
        {
            Id = a.Id,
            Type = a.Type,
            CreatedAt = a.CreatedAt,
            Payload = a.Payload,
            IsArchived = true,
            ArchivedAt = a.ArchivedAt
        });

        IQueryable<Row> query = scope switch
        {
            "archived" => archiveQuery,
            "all" => liveQuery.Concat(archiveQuery),
            _ => liveQuery
        };

        IOrderedQueryable<Row> ordered = sortBy switch
        {
            "type" => descending
                ? query.OrderByDescending(r => r.Type).ThenByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.Type).ThenBy(r => r.CreatedAt),
            "id" => descending
                ? query.OrderByDescending(r => r.Id).ThenByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.Id).ThenBy(r => r.CreatedAt),
            _ => descending
                ? query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                : query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var totalCount = await ordered.CountAsync(cancellationToken);

        var pageRows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(r => new ToolDocumentDto
        {
            Id = r.Id,
            Type = r.Type,
            CreatedAt = r.CreatedAt,
            ArchivedAt = r.ArchivedAt,
            IsArchived = r.IsArchived,
            Payload = DeserializePayload(r.Payload)
        }).ToList();

        return new GetToolDocumentsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    private static JsonElement DeserializePayload(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(payload);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { raw = payload });
        }
    }
}
