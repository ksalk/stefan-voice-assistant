using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Services;

public class RemoteServerClient(
    HttpClient httpClient,
    IOptions<NodeOptions> nodeOptions,
    IOptions<ServerOptions> serverOptions,
    ILogger<RemoteServerClient> logger)
{
    private static readonly string _sessionId = Guid.NewGuid().ToString();

    public async Task<bool> RegisterNodeAsync()
    {
        var port = new Uri(serverOptions.Value.Url).Port;
        Console.WriteLine($"Node session ID: {_sessionId}");
        var payload = new JsonObject
        {
            ["NodeName"] = nodeOptions.Value.NodeName,
            ["SessionId"] = _sessionId,
            ["Port"] = port,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/nodes/register")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        logger.LogInformation("Registering node with server at {ServerUrl}...",
            request.RequestUri);

        var response = await SendAsync(request);

        if (response is not null)
        {
            logger.LogInformation("Node registered successfully.");
            return true;
        }

        return false;
    }

    public async Task<bool> SendCommandAsync(byte[] commandAudio)
    {
        Console.WriteLine($"Node session - command ID: {_sessionId}");
        var audioContent = new ByteArrayContent(commandAudio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        var content = new MultipartFormDataContent
        {
            { audioContent, "file", "command.wav" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/commands")
        {
            Content = content
        };

        request.Headers.Add("X-Node-Device-ID", nodeOptions.Value.NodeName);
        request.Headers.Add("X-Node-Session-ID", _sessionId);

        logger.LogInformation("Sending command to server at {ServerUrl}...",
            request.RequestUri);

        var response = await SendAsync(request);

        if (response is not null)
        {
            logger.LogInformation("Command sent successfully.");
            return true;
        }

        return false;
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request)
    {
        try
        {
            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return response;

            var body = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Request to {Url} failed: server returned {StatusCode} — {Body}",
                request.RequestUri,
                (int)response.StatusCode,
                body.Trim());
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Request to {Url} failed: {Message}", request.RequestUri, ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Request to {Url} timed out after 10s.", request.RequestUri);
            return null;
        }
    }
}
