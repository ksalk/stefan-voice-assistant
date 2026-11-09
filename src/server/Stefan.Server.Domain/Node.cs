namespace Stefan.Server.Domain;

public class Node
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public string CurrentSessionId { get; private set; } = string.Empty;

    public string LastKnownIpAddress { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public NodeStatus Status { get; private set; }

    public DateTime RegisteredAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public int RestartCount { get; private set; }

    /// <summary>Wake-word detector reported by the node at registration, for example <c>nanowakeword</c> or <c>sherpa</c>.</summary>
    public string? Detector { get; private set; }

    public static Node Create(string name, string sessionId, string ipAddress, int port, string? detector = null)
    {
        // TODO: validate inputs
        return new Node
        {
            Id = Guid.NewGuid(),
            Name = name,
            CurrentSessionId = sessionId,
            LastKnownIpAddress = ipAddress,
            Port = port,
            Status = NodeStatus.Online,
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            RestartCount = 0,
            Detector = NormalizeDetector(detector)
        };
    }

    public void Connect(string sessionId, string ipAddress, int port, string? detector = null)
    {
        if (CurrentSessionId != sessionId)
        {
            CurrentSessionId = sessionId;
            RestartCount++;
        }

        LastKnownIpAddress = ipAddress;
        Port = port;
        LastSeenAt = DateTime.UtcNow;
        Status = NodeStatus.Online;

        // Keep the last known value when an older node registers without a detector.
        if (NormalizeDetector(detector) is { } normalizedDetector)
        {
            Detector = normalizedDetector;
        }
    }

    public void MarkSeen()
    {
        LastSeenAt = DateTime.UtcNow;
        Status = NodeStatus.Online;
    }

    public void MarkOffline()
    {
        Status = NodeStatus.Offline;
    }

    private static string? NormalizeDetector(string? detector) =>
        string.IsNullOrWhiteSpace(detector) ? null : detector.Trim().ToLowerInvariant();
}