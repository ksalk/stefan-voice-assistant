using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Stefan.Server.API;

public class NodeRegistry
{
    private readonly ConcurrentDictionary<string, WebSocket> _nodes = new();
    
    public void RegisterNode(string nodeId, WebSocket socket)
    {
        _nodes.TryAdd(nodeId, socket);
    }

    public void UnregisterNode(string nodeId)
    {
        _nodes.TryRemove(nodeId, out _);
    }
}