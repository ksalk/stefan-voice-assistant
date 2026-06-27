using System.Net.Http.Json;
using System.Text.Json;

namespace Stefan.Server.IntegrationTests;

public sealed class ServerAppClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;

    public ServerAppClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/health", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Health response body was null.");
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record HealthResponse(
    string Status,
    string Version,
    string CommitHash,
    string SttProvider,
    string TtsProvider);