using System.Net.Http.Headers;

namespace Stefan.Node.IntegrationTests;

public sealed class NodeAppClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public NodeAppClient(HttpClient httpClient) => _httpClient = httpClient;

    public Task<HttpResponseMessage> GetHealthAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetAsync("/health", cancellationToken);

    public Task<HttpResponseMessage> GetStatusAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetAsync("/status", cancellationToken);

    public async Task<HttpResponseMessage> PostAudioAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        return await _httpClient.PostAsync("/audio", content, cancellationToken);
    }

    public Task<HttpResponseMessage> DeleteCurrentAudioAsync(CancellationToken cancellationToken = default)
        => _httpClient.DeleteAsync("/audio/current", cancellationToken);

    public void Dispose() => _httpClient.Dispose();
}
