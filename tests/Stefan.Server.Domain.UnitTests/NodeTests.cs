namespace Stefan.Server.Domain.UnitTests;

public class NodeTests
{
    [Fact]
    public void Create_InitializesAllFields()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        Assert.NotEqual(Guid.Empty, node.Id);
        Assert.Equal("kitchen-node", node.Name);
        Assert.Equal("session-1", node.CurrentSessionId);
        Assert.Equal("192.168.1.10", node.LastKnownIpAddress);
        Assert.Equal(8080, node.Port);
        Assert.Equal(NodeStatus.Online, node.Status);
        Assert.Equal(0, node.RestartCount);
        Assert.NotEqual(default, node.RegisteredAt);
        Assert.NotEqual(default, node.LastSeenAt);
        Assert.Null(node.LastPingAt);
    }

    [Fact]
    public void Connect_WithNewSession_IncrementsRestartCount()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        node.Connect("session-2", "192.168.1.20", 9090);

        Assert.Equal("session-2", node.CurrentSessionId);
        Assert.Equal("192.168.1.20", node.LastKnownIpAddress);
        Assert.Equal(9090, node.Port);
        Assert.Equal(1, node.RestartCount);
        Assert.Equal(NodeStatus.Online, node.Status);
        Assert.NotEqual(default, node.LastSeenAt);
    }

    [Fact]
    public void Connect_WithSameSession_DoesNotIncrementRestartCount()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        node.Connect("session-1", "192.168.1.10", 8080);

        Assert.Equal(0, node.RestartCount);
        Assert.NotEqual(default, node.LastSeenAt);
    }

    [Fact]
    public void Connect_UpdatesIpAndPortRegardlessOfSession()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        node.Connect("session-1", "10.0.0.5", 3000);

        Assert.Equal("10.0.0.5", node.LastKnownIpAddress);
        Assert.Equal(3000, node.Port);
    }

    [Fact]
    public void MarkSeen_UpdatesLastSeenAtAndSetsOnline()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);
        node.MarkOffline();

        node.MarkSeen();

        Assert.Equal(NodeStatus.Online, node.Status);
        Assert.NotEqual(default, node.LastSeenAt);
    }

    [Fact]
    public void MarkPinged_UpdatesLastPingAt()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);
        Assert.Null(node.LastPingAt);

        node.MarkPinged();

        Assert.NotNull(node.LastPingAt);
    }

    [Fact]
    public void MarkOffline_SetsStatusToOffline()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        node.MarkOffline();

        Assert.Equal(NodeStatus.Offline, node.Status);
    }

    [Fact]
    public void MultipleConnectCalls_AccumulateRestartCount()
    {
        var node = Node.Create("kitchen-node", "session-1", "192.168.1.10", 8080);

        node.Connect("session-2", "192.168.1.10", 8080);
        node.Connect("session-3", "192.168.1.10", 8080);
        node.Connect("session-3", "192.168.1.10", 8080);

        Assert.Equal(2, node.RestartCount);
    }

    [Fact]
    public void FullLifecycle_CreatesConnectPingOfflineSeen()
    {
        var node = Node.Create("living-room", "s1", "10.0.0.1", 8080);
        Assert.Equal(NodeStatus.Online, node.Status);
        Assert.Equal(0, node.RestartCount);

        node.Connect("s2", "10.0.0.2", 9090);
        Assert.Equal(1, node.RestartCount);
        Assert.Equal(NodeStatus.Online, node.Status);

        node.MarkPinged();
        Assert.NotNull(node.LastPingAt);

        node.MarkOffline();
        Assert.Equal(NodeStatus.Offline, node.Status);

        node.MarkSeen();
        Assert.Equal(NodeStatus.Online, node.Status);
    }
}
