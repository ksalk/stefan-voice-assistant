var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/", () => "OK");

await app.RunAsync("http://0.0.0.0:8080");

public class Worker : BackgroundService
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