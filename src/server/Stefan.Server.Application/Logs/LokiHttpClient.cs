using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Stefan.Server.Application.Logs;

/// <summary>
/// Low level Loki HTTP API client. Knows how to call <c>/loki/api/v1/query_range</c> and parse
/// the "streams" part of the Loki response into log entries.
/// </summary>
public class LokiHttpClient(IHttpClientFactory httpClientFactory)
{
    public const string ClientName = "Loki";

    public async Task<IReadOnlyList<LokiLogEntry>> QueryRangeAsync(
        string query,
        long startNanoseconds,
        long endNanoseconds,
        int limit,
        CancellationToken cancellationToken)
    {
        var requestUri = $"loki/api/v1/query_range?query={Uri.EscapeDataString(query)}" +
                         $"&start={startNanoseconds}&end={endNanoseconds}" +
                         $"&limit={limit}&direction=backward";

        using var response = await httpClientFactory
            .CreateClient(ClientName)
            .GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("status", out var status) is false || status.ValueEquals("success") is false)
        {
            throw new InvalidOperationException("Loki returned a non-success response.");
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("resultType", out var resultType) ||
            resultType.ValueEquals("streams") is false)
        {
            throw new InvalidOperationException("Loki returned an unexpected result type (expected streams).");
        }

        var entries = new List<LokiLogEntry>();
        foreach (var streamElement in data.GetProperty("result").EnumerateArray())
        {
            var labels = new Dictionary<string, string>();
            foreach (var label in streamElement.GetProperty("stream").EnumerateObject())
            {
                labels[label.Name] = label.Value.GetString() ?? string.Empty;
            }

            foreach (var value in streamElement.GetProperty("values").EnumerateArray())
            {
                var timestamp = value[0].GetString();
                var line = value[1].GetString() ?? string.Empty;
                entries.Add(LokiLogEntry.FromNanoseconds(timestamp, line, labels));
            }
        }

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }
}

public record LokiLogEntry(DateTimeOffset Timestamp, string Line, IReadOnlyDictionary<string, string> Labels)
{
    public static LokiLogEntry FromNanoseconds(string? nanoseconds, string line, IReadOnlyDictionary<string, string> labels)
    {
        var microsecondsSinceEpoch = long.TryParse(nanoseconds, out var ns) ? ns / 1_000 : 0;
        return new LokiLogEntry(DateTimeOffset.FromUnixTimeMilliseconds(microsecondsSinceEpoch / 1_000), line, labels);
    }
}
