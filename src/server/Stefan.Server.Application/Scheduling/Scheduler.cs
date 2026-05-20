using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Stefan.Server.Application.Scheduling;

public class Scheduler(ISchedulerFactory schedulerFactory, ILogger<Scheduler> logger)
{
    public async Task ScheduleJob<TJob>(
        JobKey jobKey,
        JobDataMap jobDataMap,
        IScheduleBuilder scheduleBuilder,
        CancellationToken cancellationToken) where TJob : IJob
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        var existingJob = await scheduler.GetJobDetail(jobKey, cancellationToken);
        if (existingJob != null)
        {
            logger.LogDebug("Job already exists for job key {JobKey}", jobKey);
            return;
        }

        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
           //.WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithSchedule(scheduleBuilder)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        logger.LogInformation("Scheduled job for job key {JobKey}", jobKey);
    }
    
    public async Task DeleteJob(JobKey jobKey, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.DeleteJob(jobKey, cancellationToken);
        logger.LogInformation("Deleted job for job key {JobKey}", jobKey);
    }
    
    public async Task CancelAllJobsFromGroup(string groupName, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(groupName), cancellationToken);

        foreach (var jobKey in jobKeys)
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }
    }
}

public static class Schedule
{
    public static IScheduleBuilder Every(TimeSpan interval) => SimpleScheduleBuilder.RepeatSecondlyForever((int)interval.TotalSeconds);
    public static IScheduleBuilder OnceAfter(TimeSpan interval) => SimpleScheduleBuilder.Create().WithInterval(interval).WithRepeatCount(0);
}