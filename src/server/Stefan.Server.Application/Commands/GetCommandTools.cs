using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class GetCommandToolsRequest
{
    public Guid CommandId { get; set; }
}

public class CommandToolDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;

    /// <summary>Action this command performed on the document: created, updated or deleted.</summary>
    public string Action { get; set; } = null!;
    public DateTime ActionAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public JsonElement Payload { get; set; }
}

public class GetCommandToolsResult
{
    public List<CommandToolDto> Tools { get; set; } = [];
}

public class GetCommandTools(StefanDbContext dbContext, IToolDocumentStore documentStore)
{
    public async Task<Result<GetCommandToolsResult>> Handle(GetCommandToolsRequest request, CancellationToken cancellationToken)
    {
        var commandExists = await dbContext.CommandRecords
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.CommandId, cancellationToken);

        if (!commandExists)
        {
            return Result<GetCommandToolsResult>.Failure(Error.NotFound("Command not found"));
        }

        var documents = await documentStore.ListDocumentsByCommandAsync(request.CommandId, cancellationToken);

        var tools = documents
            .SelectMany(d => ParseCommandActions(d.CommandActions)
                .Where(a => a.CommandId == request.CommandId)
                .Select(a => new CommandToolDto
                {
                    Id = d.Id,
                    Type = d.Type,
                    Action = a.Action,
                    ActionAtUtc = a.AtUtc,
                    CreatedAt = d.CreatedAt,
                    Payload = DeserializePayload(d.Payload),
                }))
            .OrderBy(t => t.ActionAtUtc)
            .ToList();

        return Result<GetCommandToolsResult>.Success(new GetCommandToolsResult { Tools = tools });
    }

    private static List<ToolDocumentCommandAction> ParseCommandActions(string? commandActions)
    {
        if (string.IsNullOrEmpty(commandActions))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ToolDocumentCommandAction>>(commandActions) ?? [];
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
