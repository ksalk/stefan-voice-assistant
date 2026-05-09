using Microsoft.Extensions.Logging;
using Quartz;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;

namespace Stefan.Server.Application.AI.Tools.Timer;

[DisallowConcurrentExecution]
public class FireTimerJob(
    TimerDbContext dbContext,
    TextToSpeechService ttsService,
    ILogger<FireTimerJob> logger) : IJob
{
    public const string TimerIdKey = "TimerId";
    public const string DeviceIdKey = "DeviceId";
    public const string LabelKey = "Label";

    public async Task Execute(IJobExecutionContext context)
    {
        var timerId = context.MergedJobDataMap.GetInt(TimerIdKey);
        var deviceId = context.MergedJobDataMap.GetString(DeviceIdKey) ?? string.Empty;
        var label = context.MergedJobDataMap.GetString(LabelKey);

        logger.LogInformation("Timer {TimerId} fired for device {DeviceId}", timerId, deviceId);

        var message = string.IsNullOrEmpty(label)
            ? $"Timer {timerId} is done!"
            : $"Timer '{label}' is done!";

        try
        {
            var audioData = await ttsService.SynthesizeAsync(message);
            var notifier = new NodeNotifier();
            await notifier.SendAudioNotification(deviceId, audioData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send audio notification for timer {TimerId}", timerId);
        }

        // Remove the timer entry from the database
        var entry = await dbContext.Timers.FindAsync(timerId, context.CancellationToken);
        if (entry != null)
        {
            dbContext.Timers.Remove(entry);
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
