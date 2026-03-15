using System.Collections.Concurrent;
using Stefan.Server.Common;

namespace Stefan.Server.AI.Tools.Timer;

internal record RunningTimer(Task TimerTask, string DeviceId, CancellationTokenSource Cts);

public static class TimerScheduler
{
    private static readonly ConcurrentDictionary<int, RunningTimer> _runningTimers = new();

    public static void ScheduleTimer(TimerEntry entry, string deviceId)
    {
        var cts = new CancellationTokenSource();

        var task = Task.Run(async () =>
        {
            try
            {
                var secondsRemaining = entry.Seconds - (DateTime.UtcNow - entry.CreatedAt).TotalSeconds;
                await Task.Delay(TimeSpan.FromSeconds(secondsRemaining), cts.Token);

                var notifier = new NodeNotifier();
                await notifier.SendTextNotification(deviceId, $"Timer {entry.Id} finished!");

                _runningTimers.TryRemove(entry.Id, out _);
            }
            catch (TaskCanceledException) { /* cancelled by user */ }
            finally
            {
                cts.Dispose();
            }
        });

        _runningTimers[entry.Id] = new RunningTimer(task, deviceId, cts);
    }

    public static async Task CancelTimer(int timerId)
    {
        if (_runningTimers.TryRemove(timerId, out var runningTimer))
        {
            runningTimer.Cts.Cancel();
            try
            {
                await runningTimer.TimerTask;
            }
            catch (TaskCanceledException) { /* expected */ }
            // Cts is disposed inside the task's finally block
        }
    }
}

