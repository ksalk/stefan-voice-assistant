namespace Stefan.Server.Domain;

public class NodeStatusReport
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }

    public DateTime Timestamp { get; set; }
    public NodeStatus Status { get; set; }

    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public double? DiskUsage { get; set; }
}