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
        logger.LogInformation("[http] Node session ID: {SessionId}", _sessionId);
        var payload = new JsonObject
        {
            ["NodeName"] = nodeOptions.Value.Name,
            ["SessionId"] = _sessionId,
            ["Port"] = port,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/nodes/register")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        logger.LogInformation("[http] Registering node with server at {ServerUrl}...",
            request.RequestUri);

        var response = await SendAsync(request);

        if (response is not null)
        {
            logger.LogInformation("[http] Node registered successfully.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sends a voice command WAV to the server. Returns the response audio bytes on success, or null on failure.
    /// </summary>
    public async Task<byte[]?> SendCommandAsync(byte[] commandAudio)
    {
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

        request.Headers.Add("X-Node-Device-ID", nodeOptions.Value.Name);
        request.Headers.Add("X-Node-Session-ID", _sessionId);

        logger.LogInformation("[http] Sending command to server at {ServerUrl}...",
            request.RequestUri);

        var response = await SendAsync(request);
        if (response is not null)
        {
            var responseText = Uri.UnescapeDataString(response.Headers.GetValues("X-Response-Text").FirstOrDefault() ?? string.Empty);
            logger.LogInformation("[http] Command sent successfully. Received response text: {ResponseText}", responseText ?? "(none)");
            return await response.Content.ReadAsByteArrayAsync();
        }

        return null;
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
                "[http] Request to {Url} failed: server returned {StatusCode} — {Body}",
                request.RequestUri,
                (int)response.StatusCode,
                body.Trim());
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[http] Request to {Url} failed: {Message}", request.RequestUri, ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            logger.LogError("[http] Request to {Url} timed out after 10s.", request.RequestUri);
            return null;
        }
    }
}
