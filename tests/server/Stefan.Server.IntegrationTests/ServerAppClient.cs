using System.Net.Http.Json;
using System.Text;
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

    public async Task<HttpResponseMessage> PostRegisterNodeAsync(
        string? nodeSecret,
        object body,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/nodes/register");
        if (nodeSecret is not null)
            request.Headers.Add("X-Node-Secret", nodeSecret);
        request.Content = JsonContent.Create(body);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostRawRegisterAsync(
        string nodeSecret,
        string rawBody,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/nodes/register");
        request.Headers.Add("X-Node-Secret", nodeSecret);
        request.Content = new StringContent(rawBody, Encoding.UTF8, contentType);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<GetNodesResult> GetNodesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/nodes", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GetNodesResult>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("GetNodes response body was null.");
    }

    public async Task<NodeDetailsResult> GetNodeDetailsAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/nodes/{nodeId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeDetailsResult>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("GetNodeDetails response body was null.");
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record HealthResponse(
    string Status,
    string Version,
    string CommitHash,
    string SttProvider,
    string TtsProvider);

public sealed record RegisterNodeRequestDto(string NodeName, string SessionId, int Port);

public sealed record NodeSummaryDto(
    Guid Id,
    string Name,
    string CurrentSessionId,
    string LastKnownIpAddress,
    int Port,
    string Status,
    DateTime RegisteredAt,
    DateTime? LastSeenAt,
    DateTime? LastPingAt,
    int RestartCount);

public sealed record GetNodesResult(IReadOnlyList<NodeSummaryDto> Nodes);

public sealed record NodeStatusReportDto(
    DateTime Timestamp,
    double? CpuUsage,
    double? MemoryUsage,
    double? DiskUsage,
    int? AudioVolume,
    string? Version,
    string? GitCommit,
    string Status);

public sealed record NodeDetailsDto(
    Guid Id,
    string Name,
    string CurrentSessionId,
    string LastKnownIpAddress,
    int Port,
    string Status,
    DateTime RegisteredAt,
    DateTime? LastSeenAt,
    DateTime? LastPingAt,
    int RestartCount,
    IReadOnlyList<NodeStatusReportDto> StatusReports);

public sealed record NodeDetailsResult(NodeDetailsDto Node);