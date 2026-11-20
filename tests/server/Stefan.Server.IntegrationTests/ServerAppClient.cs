using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Net.Http.Headers;
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

    public async Task<HttpResponseMessage> PostCommandAsync(
        string? nodeSecret,
        string? deviceId,
        string? sessionId,
        string? commandId,
        byte[]? wakeWordAudio = null,
        byte[]? commandAudio = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/commands");
        if (nodeSecret is not null)
            request.Headers.Add("X-Node-Secret", nodeSecret);
        if (deviceId is not null)
            request.Headers.Add("X-Node-Device-ID", deviceId);
        if (sessionId is not null)
            request.Headers.Add("X-Node-Session-ID", sessionId);
        if (commandId is not null)
            request.Headers.Add("X-Command-ID", commandId);

        var audio = commandAudio ?? CreateSpeechWav(TimeSpan.FromSeconds(1));
        var content = new MultipartFormDataContent
        {
            {
                new ByteArrayContent(audio) { Headers = { ContentType = new MediaTypeHeaderValue("audio/wav") } },
                "file",
                "command.wav"
            }
        };

        if (wakeWordAudio is not null)
        {
            content.Add(
                new ByteArrayContent(wakeWordAudio) { Headers = { ContentType = new MediaTypeHeaderValue("audio/wav") } },
                "wakeWordFile",
                "wake-word.wav");
        }

        request.Content = content;

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetCommandAudioAsync(
        Guid commandId,
        string type,
        CancellationToken cancellationToken = default) =>
        await _httpClient.GetAsync($"/api/commands/{commandId}/audio?type={type}", cancellationToken);

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

    public async Task<HttpResponseMessage> ClearNodeStatusReportsAsync(
        Guid nodeId,
        int? olderThanDays = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/nodes/{nodeId}/status-reports";
        if (olderThanDays is not null)
            path += $"?olderThanDays={olderThanDays}";

        return await _httpClient.DeleteAsync(path, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetCommandToolsAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        await _httpClient.GetAsync($"/api/commands/{commandId}/tools", cancellationToken);

    public async Task<HttpResponseMessage> ArchiveToolDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _httpClient.PostAsync($"/api/tool-documents/{id}/archive", null, cancellationToken);

    public async Task<HttpResponseMessage> GetCommandLogsAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        await _httpClient.GetAsync($"/api/commands/{commandId}/logs", cancellationToken);

    public void Dispose() => _httpClient.Dispose();

    /// <summary>Builds a 16 kHz mono PCM16 WAV with a constant tone.</summary>
    public static byte[] CreateSpeechWav(
        TimeSpan duration,
        double amplitude = 0.25,
        double frequencyHz = 440) =>
        CreateWav(duration, sampleIndex =>
            amplitude * Math.Sin(2 * Math.PI * frequencyHz * sampleIndex / 16_000.0));

    /// <summary>Builds a 16 kHz mono PCM16 WAV with no signal.</summary>
    public static byte[] CreateSilentWav(TimeSpan duration) => CreateWav(duration, _ => 0);

    private static byte[] CreateWav(TimeSpan duration, Func<int, double> sample)
    {
        const int sampleRate = 16_000;
        const int channels = 1;
        const int bitsPerSample = 16;
        const int bytesPerSample = bitsPerSample / 8;

        var sampleCount = (int)(sampleRate * duration.TotalSeconds);
        var dataSize = sampleCount * channels * bytesPerSample;
        var wav = new byte[44 + dataSize];

        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4), 36 + dataSize);
        "WAVE"u8.CopyTo(wav.AsSpan(8));
        "fmt "u8.CopyTo(wav.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22), channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), sampleRate * channels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32), channels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34), bitsPerSample);
        "data"u8.CopyTo(wav.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40), dataSize);

        for (var i = 0; i < sampleCount; i++)
        {
            var value = (double)Math.Clamp(sample(i) * short.MaxValue, short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(44 + i * bytesPerSample), (short)value);
        }

        return wav;
    }
}

public sealed record HealthResponse(
    string Status,
    string Version,
    string CommitHash,
    string SttProvider,
    string TtsProvider);

public sealed record RegisterNodeRequestDto(string NodeName, string SessionId, int Port, string? Detector = null);

public sealed record NodeSummaryDto(
    Guid Id,
    string Name,
    string CurrentSessionId,
    string LastKnownIpAddress,
    int Port,
    string Status,
    DateTime RegisteredAt,
    DateTime? LastSeenAt,
    int RestartCount,
    string? Detector);

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
    int RestartCount,
    string? Detector,
    IReadOnlyList<NodeStatusReportDto> StatusReports);

public sealed record NodeDetailsResult(NodeDetailsDto Node);

public sealed record ClearNodeStatusReportsResult(int DeletedCount);

public sealed record CommandToolDto(
    Guid Id,
    string Type,
    string Action,
    DateTime ActionAtUtc,
    DateTime CreatedAt,
    JsonElement Payload);

public sealed record GetCommandToolsResult(IReadOnlyList<CommandToolDto> Tools);