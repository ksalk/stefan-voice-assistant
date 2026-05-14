namespace Stefan.Node.HttpServer;

public static class HttpServer
{
    public static async Task RunServerAsync(this WebApplication app, string url)
    {
        app.MapEndpoints();

        await app.RunAsync(url);
    }

    private static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => "OK");

        app.MapGet("/status", async (AppStateService stateService) =>
        {
            var status = new NodeStatusResponse
            {
                State = stateService.CurrentState switch
                {
                    VoiceAssistantState.RecordingCommand => "recording",
                    VoiceAssistantState.ListeningForWakeWord => "listening",
                    _ => "online"
                },
                CpuUsage = await GetCpuUsageAsync(),
                MemoryUsage = GetMemoryUsage(),
                DiskUsage = GetDiskUsage(),
                Version = ThisAssembly.AssemblyInformationalVersion,
                GitCommit = ThisAssembly.GitCommitId
            };
            return Results.Json(status, NodeJsonContext.Default.NodeStatusResponse);
        });

        app.MapPost("/play", async (HttpContext context, Stefan.Node.Audio.AudioPlayer audioPlayer) =>
        {
            IFormFile? file = context.Request.Form.Files.Count > 0
                ? context.Request.Form.Files[0]
                : null;

            if (file is null || file.Length == 0)
                return Results.BadRequest("No audio file provided.");

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(ms);

            audioPlayer.Queue(ms.ToArray());

            return Results.Ok();
        })
        .DisableAntiforgery();

        app.MapDelete("/play/current", (Stefan.Node.Audio.AudioPlayer audioPlayer) =>
        {
            audioPlayer.CancelCurrent();
            return Results.Ok();
        });

        return app;
    }

    private static async Task<double> GetCpuUsageAsync()
    {
        var (idle1, total1) = ReadCpuTimes();
        await Task.Delay(100);
        var (idle2, total2) = ReadCpuTimes();

        var totalDiff = total2 - total1;
        var idleDiff = idle2 - idle1;

        return totalDiff > 0 ? Math.Round((double)(totalDiff - idleDiff) / totalDiff * 100, 1) : 0.0;
    }

    private static (long idle, long total) ReadCpuTimes()
    {
        var line = File.ReadLines("/proc/stat").First(l => l.StartsWith("cpu "));
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var values = parts.Skip(1).Select(long.Parse).ToArray();
        return (values[3], values.Sum());
    }

    private static MemoryUsageInfo GetMemoryUsage()
    {
        var lines = File.ReadAllLines("/proc/meminfo");
        long totalKb = 0, availableKb = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:"))
                totalKb = ParseKbValue(line);
            else if (line.StartsWith("MemAvailable:"))
                availableKb = ParseKbValue(line);
        }

        var total = totalKb * 1024;
        var available = availableKb * 1024;
        var used = total - available;
        var percent = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;

        return new MemoryUsageInfo
        {
            Total = total,
            Available = available,
            Used = used,
            Percent = percent
        };
    }

    private static long ParseKbValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return long.Parse(parts[1]);
    }

    private static DiskUsageInfo GetDiskUsage()
    {
        var drive = new DriveInfo("/");
        var total = drive.TotalSize;
        var free = drive.AvailableFreeSpace;
        var used = total - free;
        var percent = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;

        return new DiskUsageInfo
        {
            Total = total,
            Free = free,
            Used = used,
            Percent = percent
        };
    }
}
