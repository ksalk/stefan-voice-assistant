using Microsoft.EntityFrameworkCore;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class GetNodeDetailsRequest
{
    public Guid NodeId { get; set; }
}

public class NodeDetailsDto
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
    public List<NodeStatusReportDto> StatusReports { get; set; } = [];
}

public class NodeStatusReportDto
{
    public DateTime Timestamp { get; set; }
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public double? DiskUsage { get; set; }
    public string Status { get; set; } = null!;
}

public class GetNodeDetailsResult
{
    public NodeDetailsDto Node { get; set; } = null!;
}

public class GetNodeDetails(StefanDbContext dbContext)
{
    public async Task<GetNodeDetailsResult> Handle(GetNodeDetailsRequest request, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes
            .AsNoTracking()
            .Where(n => n.Id == request.NodeId)
            .Select(n => new NodeDetailsDto
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
                RestartCount = n.RestartCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (node == null)
        {
            throw new Exception($"Node with ID {request.NodeId} not found");
        }

        var statusReports = await dbContext.NodeStatusReports
            .AsNoTracking()
            .Where(r => r.NodeId == request.NodeId)
            .OrderByDescending(r => r.Timestamp)
            .Select(r => new NodeStatusReportDto
            {
                Timestamp = r.Timestamp,
                CpuUsage = r.CpuUsage,
                MemoryUsage = r.MemoryUsage,
                DiskUsage = r.DiskUsage,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        node.StatusReports = statusReports;

        return new GetNodeDetailsResult { Node = node };
    }
}