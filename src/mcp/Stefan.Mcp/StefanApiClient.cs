using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Stefan.Mcp;

public sealed class StefanApiClient(HttpClient httpClient, ILogger<StefanApiClient> logger)
{
    private static readonly string[] CommandStatuses =
    [
        "Received",
        "SttFailed",
        "SttSuccess",
        "LlmFailed",
        "LlmSuccess",
        "TtsFailed",
        "TtsSuccess",
        "HttpFailed",
        "Completed",
        "Failed",
        "Canceled"
    ];

    private static readonly string[] ToolDocumentScopes = ["live", "archived", "all"];
    private static readonly string[] ToolDocumentSortFields = ["createdAt", "type", "id"];
    private static readonly string[] SortDirections = ["asc", "desc"];

    public static Uri CreateBaseAddress(string apiUrl)
    {
        if (!Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"STEFAN_API_URL must be an absolute HTTP or HTTPS URL. Got '{apiUrl}'.");
        }

        var builder = new UriBuilder(uri);
        var path = builder.Path.TrimEnd('/');

        // Accept an origin, an origin plus /api, or a copied endpoint URL such as /api/nodes.
        var apiIndex = path.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (apiIndex >= 0)
        {
            path = path[..apiIndex];
        }
        else if (path.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        builder.Path = path.TrimEnd('/') + "/api/";
        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        return builder.Uri;
    }

    public Task<string> GetHealthAsync(CancellationToken cancellationToken) =>
        GetAsync("health", cancellationToken);

    public Task<string> ListNodesAsync(CancellationToken cancellationToken) =>
        GetAsync("nodes", cancellationToken);

    public Task<string> GetNodeAsync(
        Guid nodeId,
        int maxStatusReports,
        CancellationToken cancellationToken) =>
        GetNodeCoreAsync(nodeId, maxStatusReports, cancellationToken);

    public Task<string> ListCommandsAsync(
        int page,
        int pageSize,
        Guid? nodeId,
        string[]? statuses,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = new List<string>
        {
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (nodeId.HasValue)
        {
            query.Add($"nodeId={nodeId.Value:D}");
        }

        foreach (var status in NormalizeValues(statuses, CommandStatuses, "status"))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        return GetAsync($"commands?{string.Join("&", query)}", cancellationToken);
    }

    public Task<string> GetCommandAsync(
        Guid commandId,
        bool includeLlmConversation,
        CancellationToken cancellationToken) =>
        GetCommandCoreAsync(commandId, includeLlmConversation, cancellationToken);

    public Task<string> GetCommandToolsAsync(Guid commandId, CancellationToken cancellationToken) =>
        GetAsync($"commands/{commandId:D}/tools", cancellationToken);

    public Task<string> GetCommandLogsAsync(
        Guid commandId,
        int maxEntries,
        CancellationToken cancellationToken) =>
        GetCommandLogsCoreAsync(commandId, maxEntries, cancellationToken);

    public Task<string> ListToolDocumentsAsync(
        int page,
        int pageSize,
        string? scope,
        string? type,
        string? search,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedScope = NormalizeChoice(scope, ToolDocumentScopes, "scope") ?? "live";
        var normalizedSortBy = NormalizeChoice(sortBy, ToolDocumentSortFields, "sortBy");
        var normalizedSortDirection = NormalizeChoice(sortDirection, SortDirections, "sortDirection");

        var query = new List<string>
        {
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}",
            $"scope={Uri.EscapeDataString(normalizedScope)}"
        };

        if (!string.IsNullOrWhiteSpace(type))
        {
            query.Add($"type={Uri.EscapeDataString(type.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (normalizedSortBy is not null)
        {
            query.Add($"sortBy={Uri.EscapeDataString(normalizedSortBy)}");
        }

        if (normalizedSortDirection is not null)
        {
            query.Add($"sortDirection={Uri.EscapeDataString(normalizedSortDirection)}");
        }

        return GetAsync($"tool-documents?{string.Join("&", query)}", cancellationToken);
    }

    public Task<string> GetToolDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        GetAsync($"tool-documents/{documentId:D}", cancellationToken);

    private async Task<string> GetNodeCoreAsync(
        Guid nodeId,
        int maxStatusReports,
        CancellationToken cancellationToken)
    {
        maxStatusReports = Math.Clamp(maxStatusReports, 1, 200);

        var json = await GetAsync($"nodes/{nodeId:D}", cancellationToken);
        if (JsonNode.Parse(json) is not JsonObject root ||
            root["node"] is not JsonObject node ||
            node["statusReports"] is not JsonArray statusReports)
        {
            return json;
        }

        var totalStatusReports = statusReports.Count;
        var returnedStatusReports = Math.Min(totalStatusReports, maxStatusReports);
        var trimmedReports = new JsonArray();
        for (var index = 0; index < returnedStatusReports; index++)
        {
            trimmedReports.Add(statusReports[index]?.DeepClone());
        }

        node["statusReports"] = trimmedReports;
        node["returnedStatusReports"] = returnedStatusReports;
        node["totalStatusReports"] = totalStatusReports;
        node["statusReportsTruncated"] = totalStatusReports > returnedStatusReports;
        node["maxStatusReports"] = maxStatusReports;
        return root.ToJsonString();
    }

    private async Task<string> GetCommandCoreAsync(
        Guid commandId,
        bool includeLlmConversation,
        CancellationToken cancellationToken)
    {
        var json = await GetAsync($"commands/{commandId:D}", cancellationToken);
        if (includeLlmConversation)
        {
            return json;
        }

        if (JsonNode.Parse(json) is not JsonObject root)
        {
            return json;
        }

        var conversationKey = root
            .Select(property => property.Key)
            .FirstOrDefault(key => string.Equals(key, "llmConversationJson", StringComparison.OrdinalIgnoreCase));

        if (conversationKey is not null)
        {
            root[conversationKey] = null;
        }

        return root.ToJsonString();
    }

    private async Task<string> GetCommandLogsCoreAsync(
        Guid commandId,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        maxEntries = Math.Clamp(maxEntries, 1, 500);

        var json = await GetAsync($"commands/{commandId:D}/logs", cancellationToken);
        if (JsonNode.Parse(json) is not JsonObject root || root["entries"] is not JsonArray entries)
        {
            return json;
        }

        var returnedEntries = Math.Min(entries.Count, maxEntries);
        var trimmedEntries = new JsonArray();
        for (var index = 0; index < returnedEntries; index++)
        {
            trimmedEntries.Add(entries[index]?.DeepClone());
        }

        root["entries"] = trimmedEntries;
        root["returnedEntries"] = returnedEntries;
        root["truncated"] = entries.Count > returnedEntries;
        root["maxEntries"] = maxEntries;
        return root.ToJsonString();
    }

    private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            path,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebug("Stefan API GET {Path} returned {StatusCode}", path, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var title = response.ReasonPhrase ?? "Stefan API request failed";
            var detail = string.IsNullOrWhiteSpace(body) ? "No response body." : body;
            throw new StefanApiException((int)response.StatusCode, title, Truncate(detail, 1500));
        }

        return string.IsNullOrWhiteSpace(body) ? "{}" : body;
    }

    private static IReadOnlyList<string> NormalizeValues(
        string[]? values,
        string[] allowedValues,
        string name)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var normalizedValues = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = allowedValues.FirstOrDefault(allowed =>
                string.Equals(allowed, value.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                throw new StefanApiException(
                    400,
                    "Invalid filter",
                    $"Unknown {name} '{value}'. Allowed values: {string.Join(", ", allowedValues)}.");
            }

            if (!normalizedValues.Contains(match, StringComparer.Ordinal))
            {
                normalizedValues.Add(match);
            }
        }

        return normalizedValues;
    }

    private static string? NormalizeChoice(string? value, string[] allowedValues, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = allowedValues.FirstOrDefault(allowed =>
            string.Equals(allowed, value.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new StefanApiException(
                400,
                "Invalid filter",
                $"Unknown {name} '{value}'. Allowed values: {string.Join(", ", allowedValues)}.");
        }

        return match;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
