using Microsoft.Extensions.Logging;
using Quartz;
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

[DisallowConcurrentExecution]
public class PingNodeJob : IJob
{
    private readonly StefanDbContext _dbContext;
    private readonly ILogger<PingNodeJob> _logger;
    private readonly HttpClient _httpClient;

    public PingNodeJob(StefanDbContext dbContext, ILogger<PingNodeJob> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public static readonly string NodeIdKey = "NodeId";
    public static readonly string FailureCountKey = "FailureCount";

    public async Task Execute(IJobExecutionContext context)
    {
        var nodeId = context.MergedJobDataMap.GetString(NodeIdKey);
        var failureCount = context.MergedJobDataMap.GetInt(FailureCountKey);

        var node = await _dbContext.Nodes.FindAsync(Guid.Parse(nodeId));
        if (node == null)
        {
            _logger.LogWarning("Node {NodeId} not found, removing ping job", nodeId);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        if (node.Status != Domain.NodeStatus.Online)
        {
            _logger.LogInformation("Node {NodeName} is not online, removing ping job", node.Name);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        try
        {
            var uriBuilder = new UriBuilder("http", node.LastKnownIpAddress, node.Port, "status");
            var pingUrl = uriBuilder.ToString();
            _logger.LogDebug("Pinging node {NodeName} at {PingUrl}", node.Name, pingUrl);

            var response = await _httpClient.GetAsync(pingUrl, context.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                node.MarkPinged();

                var responseContent = await response.Content.ReadAsStringAsync(context.CancellationToken);
                var statusData = JsonSerializer.Deserialize<NodeStatusResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (statusData != null)
                {
                    var statusReport = new NodeStatusReport
                    {
                        Id = Guid.NewGuid(),
                        NodeId = node.Id,
                        Timestamp = DateTime.UtcNow,
                        Status = ParseNodeStatus(statusData.State),
                        CpuUsage = statusData.CpuUsage,
                        MemoryUsage = statusData.MemoryUsage?.Percent,
                        DiskUsage = statusData.DiskUsage?.Percent
                    };

                    _dbContext.NodeStatusReports.Add(statusReport);
                    _logger.LogDebug("Node {NodeName} status report saved - CPU: {CpuUsage}%, Memory: {MemoryUsage}%, Disk: {DiskUsage}%", 
                        node.Name, statusData.CpuUsage, statusData.MemoryUsage?.Percent, statusData.DiskUsage?.Percent);
                }

                await _dbContext.SaveChangesAsync(context.CancellationToken);

                if (failureCount > 0)
                {
                    await UpdateFailureCount(context, 0);
                }

                _logger.LogDebug("Node {NodeName} ping successful", node.Name);
            }
            else
            {
                await HandlePingFailure(context, node, failureCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ping node {NodeName}", node.Name);
            await HandlePingFailure(context, node, failureCount);
        }
    }

    private async Task HandlePingFailure(IJobExecutionContext context, Domain.Node node, int failureCount)
    {
        failureCount++;

        if (failureCount >= 2)
        {
            _logger.LogWarning("Node {NodeName} failed 2 consecutive pings, marking offline", node.Name);
            node.MarkOffline();
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
        }
        else
        {
            _logger.LogWarning("Node {NodeName} ping failed ({FailureCount}/2), will retry in 15 minutes", node.Name, failureCount);
            await UpdateFailureCount(context, failureCount);
        }
    }

    private async Task UpdateFailureCount(IJobExecutionContext context, int failureCount)
    {
        var newJobDetail = context.JobDetail.GetJobBuilder()
            .UsingJobData(FailureCountKey, failureCount)
            .StoreDurably()
            .Build();

        await context.Scheduler.AddJob(newJobDetail, replace: true, context.CancellationToken);
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
