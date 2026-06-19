namespace Stefan.Node.HttpServer;

public class NodeStatusResponse
{
    public string State { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public MemoryUsageInfo MemoryUsage { get; set; } = new();
    public DiskUsageInfo DiskUsage { get; set; } = new();
    public int? AudioVolume { get; set; }
    public string? Version { get; set; }
    public string? GitCommit { get; set; }
}
