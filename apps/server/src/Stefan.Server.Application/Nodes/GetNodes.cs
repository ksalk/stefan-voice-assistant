using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class GetNodesRequest;

public class NodeSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrentSessionId { get; set; } = null!;
    public string LastKnownIpAddress { get; set; } = null!;
    public int Port { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastPingAt { get; set; }
    public int RestartCount { get; set; }
}

public class GetNodesResult
{
    public List<NodeSummaryDto> Nodes { get; set; } = [];
}

public class GetNodes(StefanDbContext dbContext)
{
    public async Task<GetNodesResult> Handle(GetNodesRequest request, CancellationToken cancellationToken)
    {
        var nodes = await dbContext.Nodes
            .AsNoTracking()
            .OrderBy(n => n.Name)
            .Select(n => new NodeSummaryDto
            {
                Id = n.Id,
                Name = n.Name,
                CurrentSessionId = n.CurrentSessionId,
                LastKnownIpAddress = n.LastKnownIpAddress,
                Port = n.Port,
                Status = n.Status.ToString(),
                RegisteredAt = n.RegisteredAt,
                LastSeenAt = n.LastSeenAt,
                LastPingAt = n.LastPingAt,
                RestartCount = n.RestartCount,
            })
            .ToListAsync(cancellationToken);

        return new GetNodesResult { Nodes = nodes };
    }
}