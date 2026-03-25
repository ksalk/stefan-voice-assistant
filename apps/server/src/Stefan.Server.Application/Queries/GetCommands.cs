using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Queries;

public class GetCommandsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CommandSummaryDto
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = null!;
    public string SessionId { get; set; } = null!;
    public DateTime ReceivedAt { get; set; }
    public string InputAudioFormat { get; set; } = null!;
    public double InputAudioDurationMs { get; set; }
    public string Transcript { get; set; } = null!;
    public string ResponseText { get; set; } = null!;
    public string OutputAudioFormat { get; set; } = null!;
    public double SttDurationMs { get; set; }
    public double LlmDurationMs { get; set; }
    public double TtsDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}

public class GetCommandsResult
{
    public List<CommandSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetCommands(StefanDbContext dbContext)
{
    private const int MaxPageSize = 100;

    public async Task<GetCommandsResult> Handle(GetCommandsRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Min(MaxPageSize, Math.Max(1, request.PageSize));

        var query = dbContext.CommandRecords
            .AsNoTracking()
            .OrderByDescending(r => r.ReceivedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CommandSummaryDto
            {
                Id = r.Id,
                NodeId = r.NodeId,
                NodeName = dbContext.Nodes.FirstOrDefault(n => n.Id == r.NodeId)!.Name,
                SessionId = r.SessionId,
                ReceivedAt = r.ReceivedAt,
                InputAudioFormat = r.InputAudioFormat,
                InputAudioDurationMs = r.InputAudioDurationMs,
                Transcript = r.Transcript,
                ResponseText = r.ResponseText,
                OutputAudioFormat = r.OutputAudioFormat,
                SttDurationMs = r.SttDurationMs,
                LlmDurationMs = r.LlmDurationMs,
                TtsDurationMs = r.TtsDurationMs,
                TotalDurationMs = r.TotalDurationMs,
                Status = r.Status,
                ErrorMessage = r.ErrorMessage,
            })
            .ToListAsync(cancellationToken);

        return new GetCommandsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
