using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.Timer.Jobs;

[DisallowConcurrentExecution]
public class FireTimerJob(
    ToolsDbContext dbContext,
    StefanDbContext stefanDbContext,
    //ITextToSpeechService ttsService,
    NodeHttpClient nodeHttpClient,
    ILogger<FireTimerJob> logger) : IJob
{
    public const string TimerIdKey = "TimerId";
    public const string DeviceIdKey = "DeviceId";
    public const string LabelKey = "Label";
    public static readonly string JobGroup = "FireTimer";

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
            // var ttsResult = await ttsService.SynthesizeAsync(message);
            // if(!ttsResult.IsSuccess)
            // {
            //     logger.LogError("TTS synthesis failed for timer {TimerId}: {ErrorMessage}", timerId, ttsResult.Error);
            //     return;
            // }
            
            var node = await stefanDbContext.Nodes.FirstOrDefaultAsync(n => n.Name == deviceId, context.CancellationToken);
            if (node != null)
            {
                await nodeHttpClient.SendTimerAlert(node, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send audio notification for timer {TimerId}", timerId);
        }

        // Remove the timer entry from the database
        var entry = await dbContext.TimerEntries.FindAsync(timerId, context.CancellationToken);
        if (entry != null)
        {
            dbContext.TimerEntries.Remove(entry);
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
