namespace Stefan.Server.Domain;

public class Node
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    public string CurrentSessionId { get; private set; }

    public string LastKnownIpAddress { get; private set; }
    public int Port { get; private set; }

    public NodeStatus Status { get; private set; }

    public DateTime RegisteredAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public DateTime? LastPingAt { get; private set; }
    public int RestartCount { get; private set; }

    public static Node Create(string name, string sessionId, string ipAddress, int port)
    {
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
            RestartCount = 0
        };
    }

    public void Connect(string sessionId, string ipAddress, int port)
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
    }

    public void MarkSeen()
    {
        LastSeenAt = DateTime.UtcNow;
        Status = NodeStatus.Online;
    }

    public void MarkPinged()
    {
        LastPingAt = DateTime.UtcNow;
    }

    public void MarkOffline()
    {
        Status = NodeStatus.Offline;
    }
}