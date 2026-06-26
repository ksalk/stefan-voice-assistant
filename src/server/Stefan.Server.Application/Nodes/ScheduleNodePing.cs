using Microsoft.Extensions.Logging;
using Quartz;
using Stefan.Server.Application.Nodes.Jobs;
using Stefan.Server.Application.Scheduling;

namespace Stefan.Server.Application.Nodes;

public class ScheduleNodePing(Scheduler scheduler, ILogger<ScheduleNodePing> logger)
{
    public async Task Handle(Guid nodeId, CancellationToken cancellationToken)
    {
        var jobDataMap = new JobDataMap
        {
            [PingNodeJob.NodeIdKey] = nodeId,
            [PingNodeJob.FailureCountKey] = 0
        };
        var jobKey = new JobKey($"PingNode-{nodeId}", PingNodeJob.JobGroup);

        await scheduler.ScheduleJob<PingNodeJob>(jobKey, jobDataMap, Schedule.Every(TimeSpan.FromMinutes(5)), TimeSpan.FromMinutes(5), cancellationToken);
        logger.LogInformation("Scheduled ping job for node {NodeId}", nodeId);
    }
}
