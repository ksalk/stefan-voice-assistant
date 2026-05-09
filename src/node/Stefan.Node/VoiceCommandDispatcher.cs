public class VoiceCommandDispatcher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // background logic

            await Task.Delay(1000, stoppingToken);
        }
    }
}