using Quartz;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class TimerScheduler(ISchedulerFactory schedulerFactory) : ITimerScheduler
{
    private const string JobGroup = "Timers";

    public async Task ScheduleTimerAsync(TimerEntry entry, string deviceId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = GetJobKey(entry.Id);

        var jobDataMap = new JobDataMap
        {
            [FireTimerJob.TimerIdKey] = entry.Id,
            [FireTimerJob.DeviceIdKey] = deviceId,
            [FireTimerJob.LabelKey] = entry.Label ?? string.Empty,
        };

        var job = JobBuilder.Create<FireTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData(jobDataMap)
            .StoreDurably()
            .Build();

        var fireAt = entry.CreatedAt.AddSeconds(entry.Seconds);

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"TimerTrigger-{entry.Id}", JobGroup)
            .ForJob(jobKey)
            .StartAt(fireAt)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    public async Task CancelTimerAsync(int timerId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.DeleteJob(GetJobKey(timerId), cancellationToken);
    }

    private static JobKey GetJobKey(int timerId) => new($"Timer-{timerId}", JobGroup);
}
