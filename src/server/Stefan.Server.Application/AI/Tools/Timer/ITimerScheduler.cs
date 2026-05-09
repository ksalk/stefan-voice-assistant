namespace Stefan.Server.Application.AI.Tools.Timer;

public interface ITimerScheduler
{
    Task ScheduleTimerAsync(TimerEntry entry, string deviceId, CancellationToken cancellationToken = default);
    Task CancelTimerAsync(int timerId, CancellationToken cancellationToken = default);
}
