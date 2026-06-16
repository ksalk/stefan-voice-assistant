using Microsoft.Extensions.Logging;
using Stefan.Server.Domain;

namespace Stefan.Server.Application.Services;

public class NodeHttpClient(HttpClient httpClient, ILogger<NodeHttpClient> logger)
{
    public async Task<HttpResponseMessage> PingNodeAsync(Node node, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder("http", node.LastKnownIpAddress, node.Port, "ping");
        var pingUrl = uriBuilder.ToString();
        logger.LogDebug("Pinging node {NodeName} at {PingUrl}", node.Name, pingUrl);

        var response = await httpClient.GetAsync(pingUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        return response;
    }

    public async Task<HttpResponseMessage> SendTimerAlert(Node node, CancellationToken cancellationToken)
    {
        var alertAudioFilePath = Path.Combine(AppContext.BaseDirectory, "Tools", "Timer", "alarm-sound.wav");
        var alertAudioBytes = await File.ReadAllBytesAsync(alertAudioFilePath);

        return await SendWavAudio(node, alertAudioBytes, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWavAudio(Node node, byte[] audioBytes, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder("http", node.LastKnownIpAddress, node.Port, "audio");
        var sendAudioUrl = uriBuilder.ToString();
        logger.LogDebug("Sending command response audio to node {NodeName} at {SendAudioUrl}", node.Name, sendAudioUrl);

        var requestContent = new ByteArrayContent(audioBytes);
        requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

        var response = await httpClient.PostAsync(sendAudioUrl, requestContent, cancellationToken);

        response.EnsureSuccessStatusCode();
        return response;
    }
}