namespace Stefan.Node.HttpServer;

public class NodeStatusResponse
{
    public string State { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public MemoryUsageInfo MemoryUsage { get; set; } = new();
    public DiskUsageInfo DiskUsage { get; set; } = new();
}
