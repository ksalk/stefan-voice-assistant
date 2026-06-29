using Microsoft.EntityFrameworkCore;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class GetCommandRequest
{
    public Guid Id { get; set; }
}

public class GetCommand(StefanDbContext dbContext)
{
    public async Task<CommandSummaryDto?> Handle(GetCommandRequest request, CancellationToken cancellationToken)
    {
        var command = await dbContext.CommandRecords
            .AsNoTracking()
            .Where(r => r.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);

        return command;
    }
}
