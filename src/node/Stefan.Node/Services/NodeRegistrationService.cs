using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Services;

public class NodeRegistrationService(
    HttpClient httpClient,
    IOptions<ServerOptions> serverOptions,
    IOptions<RemoteServerOptions> remoteServerOptions,
    ILogger<NodeRegistrationService> logger)
{
    private readonly string _sessionId = Guid.NewGuid().ToString();

    public async Task<bool> RegisterNodeAsync()
    {
        var port = new Uri(serverOptions.Value.Url).Port;

        var payload = new JsonObject
        {
            ["NodeName"] = remoteServerOptions.Value.NodeName,
            ["SessionId"] = _sessionId,
            ["Port"] = port,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/nodes/register")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        try
        {
            logger.LogInformation(
                "Registering node with server at {ServerUrl}...",
                request.RequestUri);

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Node registered successfully.");
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Node registration failed: server returned {StatusCode} — {Body}",
                (int)response.StatusCode,
                body.Trim());
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Node registration failed: {Message}", ex.Message);
            return false;
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Node registration timed out after 10s.");
            return false;
        }
    }
}
