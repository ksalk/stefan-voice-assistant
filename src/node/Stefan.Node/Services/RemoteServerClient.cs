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

    public async Task<Result> RegisterNodeAsync()
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

        var result = await SendRequestAsync(request);
        if (!result.IsSuccess)
            return Result.Failure(result.Error!);

        result.Value.Dispose();
        logger.LogInformation("[http] Node registered successfully.");
        return Result.Success();
    }

    public async Task<Result<CommandResponse>> SendCommandAsync(byte[] commandAudio)
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

        var result = await SendRequestAsync(request);
        if (!result.IsSuccess)
            return Result<CommandResponse>.Failure(result.Error!);

        using var response = result.Value;
        var rawText = response.Headers.TryGetValues("X-Response-Text", out var values)
            ? values.FirstOrDefault() ?? string.Empty
            : string.Empty;
        var responseText = Uri.UnescapeDataString(rawText);
        var audio = await response.Content.ReadAsByteArrayAsync();

        logger.LogInformation("[http] Command sent successfully. Received response text: {ResponseText}",
            responseText == string.Empty ? "(none)" : responseText);

        return Result<CommandResponse>.Success(new CommandResponse(audio, responseText));
    }

    private async Task<Result<HttpResponseMessage>> SendRequestAsync(HttpRequestMessage request)
    {
        try
        {
            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return Result<HttpResponseMessage>.Success(response);

            var body = await response.Content.ReadAsStringAsync();
            var error = $"Server returned {(int)response.StatusCode} — {body.Trim()}";
            logger.LogError("[http] Request to {Url} failed: {Error}",
                request.RequestUri, error);
            response.Dispose();
            return Result<HttpResponseMessage>.Failure(error);
        }
        catch (HttpRequestException ex)
        {
            var error = $"Network error: {ex.Message}";
            logger.LogError(ex, "[http] Request to {Url} failed: {Error}",
                request.RequestUri, error);
            return Result<HttpResponseMessage>.Failure(error);
        }
        catch (TaskCanceledException)
        {
            var error = $"Timed out after {httpClient.Timeout.TotalSeconds}s";
            logger.LogError("[http] Request to {Url} failed: {Error}",
                request.RequestUri, error);
            return Result<HttpResponseMessage>.Failure(error);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error: {ex.Message}";
            logger.LogError(ex, "[http] Request to {Url} failed: {Error}",
                request.RequestUri, error);
            return Result<HttpResponseMessage>.Failure(error);
        }
    }
}
