using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Stefan.Server.Common;

namespace Stefan.Server.API;

public class NodeWebSocketHandler(NodeRegistry nodeRegistry, IConfiguration config)
{
    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var expectedSecret = config["NodeSecret"];
        var providedSecret = context.Request.Headers["X-Node-Secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(expectedSecret) || providedSecret != expectedSecret)
        {
            ConsoleLog.Write(LogCategory.WS, "WebSocket connection rejected: invalid or missing X-Node-Secret");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ConsoleLog.Write(LogCategory.WS, "WebSocket connection request received");

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        var buffer = new byte[1024 * 4];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var raw = Encoding.UTF8.GetString(ms.ToArray());
        var message = JsonSerializer.Deserialize<JsonElement>(raw);
        var nodeId = message.GetProperty("payload").GetProperty("nodeId").GetString()!;

        ConsoleLog.Write(LogCategory.WS, $"Node connected: {nodeId}");
        nodeRegistry.RegisterNode(nodeId, webSocket);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                    break;
                }

                raw = Encoding.UTF8.GetString(ms.ToArray());
                ConsoleLog.Write(LogCategory.WS, $"[{nodeId}] {raw}");
            }
        }
        catch (WebSocketException ex)
        {
            ConsoleLog.Write(LogCategory.WS, $"Node {nodeId} disconnected abruptly: {ex.Message}");
        }
        finally
        {
            nodeRegistry.UnregisterNode(nodeId);
            ConsoleLog.Write(LogCategory.WS, $"Node disconnected: {nodeId}");
        }
    }
}
