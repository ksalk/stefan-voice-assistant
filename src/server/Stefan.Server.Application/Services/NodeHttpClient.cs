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

    public async Task<HttpResponseMessage> SendWavAudio(Node node, byte[] audioBytes, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder("http", node.LastKnownIpAddress, node.Port, "audio");
        var sendAudioUrl = uriBuilder.ToString();
        logger.LogDebug("Sending audio to node {NodeName} at {SendAudioUrl}", node.Name, sendAudioUrl);

        using var formContent = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        formContent.Add(audioContent, "audio", "audio.wav");

        var response = await httpClient.PostAsync(sendAudioUrl, formContent, cancellationToken);

        response.EnsureSuccessStatusCode();
        return response;
    }
}