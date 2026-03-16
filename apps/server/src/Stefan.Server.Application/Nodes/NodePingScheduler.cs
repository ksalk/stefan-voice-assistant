using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Stefan.Server.Application.Nodes;

public interface INodePingScheduler
{
    Task SchedulePingAsync(Guid nodeId, CancellationToken cancellationToken = default);
    Task CancelPingAsync(Guid nodeId, CancellationToken cancellationToken = default);
    Task RescheduleAllOnlineNodesAsync(CancellationToken cancellationToken = default);
}

public class NodePingScheduler : INodePingScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<NodePingScheduler> _logger;
    private const int PingIntervalMinutes = 1;

    public NodePingScheduler(ISchedulerFactory schedulerFactory, ILogger<NodePingScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task SchedulePingAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = GetJobKey(nodeId);
        var triggerKey = GetTriggerKey(nodeId);

        var existingJob = await scheduler.GetJobDetail(jobKey, cancellationToken);
        if (existingJob != null)
        {
            _logger.LogDebug("Ping job already exists for node {NodeId}", nodeId);
            return;
        }

        var jobDataMap = new JobDataMap
        {
            [PingNodeJob.NodeIdKey] = nodeId,
            [PingNodeJob.FailureCountKey] = 0
        };

        var job = JobBuilder.Create<PingNodeJob>()
            .WithIdentity(jobKey)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(PingIntervalMinutes)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        _logger.LogInformation("Scheduled ping job for node {NodeId} (every {PingIntervalMinutes} minutes)", nodeId, PingIntervalMinutes);
    }

    public async Task CancelPingAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = GetJobKey(nodeId);
        var deleted = await scheduler.DeleteJob(jobKey, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation("Cancelled ping job for node {NodeId}", nodeId);
        }
    }

    public async Task RescheduleAllOnlineNodesAsync(CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        _logger.LogInformation("Rescheduling ping jobs for all online nodes after restart");

        // Delete all existing node ping jobs first
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("NodePings"), cancellationToken);
        foreach (var jobKey in jobKeys)
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        _logger.LogInformation("Cleared {Count} existing ping jobs", jobKeys.Count);
    }

    private static JobKey GetJobKey(Guid nodeId) => new($"PingNode-{nodeId}", "NodePings");
    private static TriggerKey GetTriggerKey(Guid nodeId) => new($"PingNodeTrigger-{nodeId}", "NodePings");
}
