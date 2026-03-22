using Microsoft.Extensions.Logging;
using Quartz;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes.Scheduling;

[DisallowConcurrentExecution]
public class PingNodeJob(StefanDbContext dbContext, ILogger<PingNodeJob> logger, PingNode pingNode) : IJob
{
    public static readonly string NodeIdKey = "NodeId";
    public static readonly string FailureCountKey = "FailureCount";

    public async Task Execute(IJobExecutionContext context)
    {
        var nodeId = context.MergedJobDataMap.GetString(NodeIdKey);
        var failureCount = context.MergedJobDataMap.GetInt(FailureCountKey);

        var result = await pingNode.Handle(new PingNodeRequest { NodeId = Guid.Parse(nodeId!) }, context.CancellationToken);

        if (result.ErrorMessage == "Node not found")
        {
            logger.LogWarning("Node {NodeId} not found, removing ping job", nodeId);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        if (result.ErrorMessage == "Node is not online")
        {
            logger.LogInformation("Node {NodeId} is not online, removing ping job", nodeId);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        if (result.Success)
        {
            if (failureCount > 0)
            {
                await UpdateFailureCount(context, 0);
            }

            logger.LogDebug("Node {NodeId} ping successful", nodeId);
        }
        else
        {
            await HandlePingFailure(context, nodeId!, failureCount);
        }
    }

    private async Task HandlePingFailure(IJobExecutionContext context, string nodeId, int failureCount)
    {
        failureCount++;

        if (failureCount >= 2)
        {
            var node = await dbContext.Nodes.FindAsync(Guid.Parse(nodeId));
            if (node != null)
            {
                logger.LogWarning("Node {NodeName} failed 2 consecutive pings, marking offline", node.Name);
                node.MarkOffline();
                await dbContext.SaveChangesAsync(context.CancellationToken);
            }

            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
        }
        else
        {
            logger.LogWarning("Node {NodeId} ping failed ({FailureCount}/2), will retry in 15 minutes", nodeId, failureCount);
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
}
