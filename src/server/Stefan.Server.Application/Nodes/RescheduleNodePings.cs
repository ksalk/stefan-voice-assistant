using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Stefan.Server.Application.Nodes.Jobs;
using Stefan.Server.Application.Scheduling;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class RescheduleNodePings(StefanDbContext dbContext, Scheduler scheduler, ILogger<RescheduleNodePings> logger)
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        // Clear any orphaned jobs first, then reschedule online nodes
        await scheduler.CancelAllJobsFromGroup(PingNodeJob.JobGroup, cancellationToken);
        logger.LogInformation("Cleared all existing node ping jobs");

        var onlineNodes = await dbContext.Nodes
            .Where(n => n.Status == NodeStatus.Online)
            .ToListAsync(cancellationToken);

        foreach (var node in onlineNodes)
        {
            var jobDataMap = new JobDataMap
            {
                [PingNodeJob.NodeIdKey] = node.Id,
                [PingNodeJob.FailureCountKey] = 0
            };
            var jobKey = new JobKey($"PingNode-{node.Id}", PingNodeJob.JobGroup);
            await scheduler.ScheduleJob<PingNodeJob>(jobKey, jobDataMap, Schedule.Every(TimeSpan.FromMinutes(5)), TimeSpan.FromMinutes(1), cancellationToken);
        }
        logger.LogInformation("Rescheduled ping jobs for {Count} online nodes", onlineNodes.Count);
    }
}
