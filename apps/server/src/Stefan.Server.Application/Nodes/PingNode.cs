using Microsoft.Extensions.Logging;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;
using System.Text.Json;

namespace Stefan.Server.Application.Nodes;

public class NodeStatusResponse
{
    public string State { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public MemoryUsageInfo MemoryUsage { get; set; } = new();
    public DiskUsageInfo DiskUsage { get; set; } = new();
}

public class MemoryUsageInfo
{
    public double Percent { get; set; }
    public long Used { get; set; }
    public long Total { get; set; }
    public long Available { get; set; }
}

public class DiskUsageInfo
{
    public double Percent { get; set; }
    public long Used { get; set; }
    public long Total { get; set; }
    public long Free { get; set; }
}

public class PingNodeRequest
{
    public Guid NodeId { get; set; }
}

public class PingNodeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public NodeStatusReport? StatusReport { get; set; }
}

public class PingNode(StefanDbContext dbContext, ILogger<PingNode> logger)
{
    private static readonly HttpClient HttpClient = new();

    public async Task<PingNodeResult> Handle(PingNodeRequest request, CancellationToken cancellationToken)
    {
        var node = await dbContext.Nodes.FindAsync([request.NodeId], cancellationToken);
        if (node == null)
        {
            return new PingNodeResult { ErrorMessage = "Node not found" };
        }

        if (node.Status != NodeStatus.Online)
        {
            return new PingNodeResult { ErrorMessage = "Node is not online" };
        }

        try
        {
            var uriBuilder = new UriBuilder("http", node.LastKnownIpAddress, node.Port, "status");
            var pingUrl = uriBuilder.ToString();
            logger.LogDebug("Pinging node {NodeName} at {PingUrl}", node.Name, pingUrl);

            var response = await HttpClient.GetAsync(pingUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Node {NodeName} returned status code {StatusCode}", node.Name, response.StatusCode);
                return new PingNodeResult { ErrorMessage = $"Node returned status code {response.StatusCode}" };
            }

            node.MarkPinged();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusData = JsonSerializer.Deserialize<NodeStatusResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            NodeStatusReport? statusReport = null;

            if (statusData != null)
            {
                statusReport = new NodeStatusReport
                {
                    Id = Guid.NewGuid(),
                    NodeId = node.Id,
                    Timestamp = DateTime.UtcNow,
                    Status = ParseNodeStatus(statusData.State),
                    CpuUsage = statusData.CpuUsage,
                    MemoryUsage = statusData.MemoryUsage?.Percent,
                    DiskUsage = statusData.DiskUsage?.Percent
                };

                dbContext.NodeStatusReports.Add(statusReport);
                logger.LogDebug("Node {NodeName} status report saved - CPU: {CpuUsage}%, Memory: {MemoryUsage}%, Disk: {DiskUsage}%",
                    node.Name, statusData.CpuUsage, statusData.MemoryUsage?.Percent, statusData.DiskUsage?.Percent);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Node {NodeName} ping successful", node.Name);

            return new PingNodeResult
            {
                Success = true,
                StatusReport = statusReport
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ping node {NodeName}", node.Name);
            return new PingNodeResult { ErrorMessage = "Failed to reach node" };
        }
    }

    private static NodeStatus ParseNodeStatus(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "offline" => NodeStatus.Offline,
            _ => NodeStatus.Online
        };
    }
}
