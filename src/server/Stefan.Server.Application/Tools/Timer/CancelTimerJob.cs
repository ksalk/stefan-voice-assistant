using Quartz;
using Stefan.Server.Application.Scheduling;
using Stefan.Server.Application.Tools.Timer.Jobs;

namespace Stefan.Server.Application.Tools.Timer;

public class CancelTimerJob(Scheduler scheduler)
{
    public async Task Handle(Guid timerId, CancellationToken cancellationToken = default)
    {
        await scheduler.DeleteJob(GetJobKey(timerId), cancellationToken);
    }

    private static JobKey GetJobKey(Guid timerId) => new($"Timer-{timerId}", FireTimerJob.JobGroup);
}
