using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class GetToolDocumentRequest
{
    public Guid Id { get; set; }
}

public class ToolDocumentCommandActionDto
{
    public Guid CommandId { get; set; }
    public string Action { get; set; } = null!;
    public DateTime AtUtc { get; set; }
}

public class ToolDocumentDetailsDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
    public JsonElement Payload { get; set; }
    public List<ToolDocumentCommandActionDto> CommandActions { get; set; } = [];
}

public class GetToolDocumentResult
{
    public ToolDocumentDetailsDto Document { get; set; } = null!;
}

public class GetToolDocument(StefanDbContext dbContext)
{
    public async Task<Result<GetToolDocumentResult>> Handle(GetToolDocumentRequest request, CancellationToken cancellationToken)
    {
        var live = await dbContext.ToolDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (live != null)
        {
            return Result<GetToolDocumentResult>.Success(new GetToolDocumentResult
            {
                Document = new ToolDocumentDetailsDto
                {
                    Id = live.Id,
                    Type = live.Type,
                    CreatedAt = live.CreatedAt,
                    ArchivedAt = null,
                    IsArchived = false,
                    Payload = DeserializePayload(live.Payload),
                    CommandActions = ParseCommandActions(live.CommandActions)
                }
            });
        }

        var archived = await dbContext.ToolDocumentArchive
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (archived != null)
        {
            return Result<GetToolDocumentResult>.Success(new GetToolDocumentResult
            {
                Document = new ToolDocumentDetailsDto
                {
                    Id = archived.Id,
                    Type = archived.Type,
                    CreatedAt = archived.CreatedAt,
                    ArchivedAt = archived.ArchivedAt,
                    IsArchived = true,
                    Payload = DeserializePayload(archived.Payload),
                    CommandActions = ParseCommandActions(archived.CommandActions)
                }
            });
        }

        return Result<GetToolDocumentResult>.Failure(Error.NotFound("Tool document not found"));
    }

    private static List<ToolDocumentCommandActionDto> ParseCommandActions(string? commandActions)
    {
        if (string.IsNullOrWhiteSpace(commandActions))
        {
            return [];
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<List<ToolDocumentCommandAction>>(commandActions, options) ?? [];
            return parsed.Select(a => new ToolDocumentCommandActionDto
            {
                CommandId = a.CommandId,
                Action = a.Action,
                AtUtc = a.AtUtc
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
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
