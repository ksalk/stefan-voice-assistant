using Quartz;
using Stefan.Server.Application.Scheduling;
using Stefan.Server.Application.Tools.Timer.Jobs;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Application.Tools.Timer;

public class ScheduleTimerJob(Scheduler scheduler)
{
    public async Task Handle(TimerEntry entry, string deviceId, CancellationToken cancellationToken = default)
    {
        var jobKey = GetJobKey(entry.Id);

        var jobDataMap = new JobDataMap
        {
            [FireTimerJob.TimerIdKey] = entry.Id,
            [FireTimerJob.DeviceIdKey] = deviceId,
            [FireTimerJob.LabelKey] = entry.Label ?? string.Empty,
        };

        await scheduler.ScheduleJob<FireTimerJob>(jobKey, jobDataMap, Schedule.OnceAfter(TimeSpan.FromSeconds(entry.DurationInSeconds)), cancellationToken);
    }

    private static JobKey GetJobKey(Guid timerId) => new($"Timer-{timerId}", FireTimerJob.JobGroup);
}
