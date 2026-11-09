using Microsoft.EntityFrameworkCore;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class ClearNodeStatusReportsRequest
{
    public Guid NodeId { get; set; }
    public int OlderThanDays { get; set; } = 30;
}

public class ClearNodeStatusReportsResult
{
    public int DeletedCount { get; set; }
}

public class ClearNodeStatusReports(StefanDbContext dbContext)
{
    private const int MinOlderThanDays = 1;
    private const int MaxOlderThanDays = 3650;

    public async Task<Result<ClearNodeStatusReportsResult>> Handle(
        ClearNodeStatusReportsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OlderThanDays < MinOlderThanDays || request.OlderThanDays > MaxOlderThanDays)
        {
            return Result<ClearNodeStatusReportsResult>.Failure(
                Error.Validation($"olderThanDays must be between {MinOlderThanDays} and {MaxOlderThanDays}."));
        }

        var nodeExists = await dbContext.Nodes
            .AnyAsync(n => n.Id == request.NodeId, cancellationToken);

        if (!nodeExists)
        {
            return Result<ClearNodeStatusReportsResult>.Failure(Error.NotFound("Node not found."));
        }

        var cutoff = DateTime.UtcNow.AddDays(-request.OlderThanDays);

        var deletedCount = await dbContext.NodeStatusReports
            .Where(r => r.NodeId == request.NodeId && r.Timestamp < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return Result<ClearNodeStatusReportsResult>.Success(new ClearNodeStatusReportsResult
        {
            DeletedCount = deletedCount
        });
    }
}
