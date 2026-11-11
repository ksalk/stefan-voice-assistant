using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Stefan.Mcp.Tools;

[McpServerToolType]
public sealed class StefanTools(StefanApiClient api)
{
    [McpServerTool(Name = "health")]
    [Description("Get Stefan server health, version, commit, configured STT/TTS providers, and LLM model.")]
    public Task<string> Health(CancellationToken cancellationToken = default) =>
        Call(() => api.GetHealthAsync(cancellationToken), cancellationToken);

    [McpServerTool(Name = "list_nodes")]
    [Description("List registered edge nodes with status, last seen time, IP, port, restart count, and detector.")]
    public Task<string> ListNodes(CancellationToken cancellationToken = default) =>
        Call(() => api.ListNodesAsync(cancellationToken), cancellationToken);

    [McpServerTool(Name = "get_node")]
    [Description("Get one edge node with its recent status reports, including CPU, memory, disk, audio volume, version, and commit.")]
    public Task<string> GetNode(
        [Description("Node id (GUID).")] Guid nodeId,
        [Description("Maximum status reports to return, from 1 to 200. The server returns newest first.")] int maxStatusReports = 20,
        CancellationToken cancellationToken = default) =>
        Call(() => api.GetNodeAsync(nodeId, maxStatusReports, cancellationToken), cancellationToken);

    [McpServerTool(Name = "list_commands")]
    [Description("List voice commands, newest first. Returns transcripts, responses, timings, status, and errors. Start with pageSize 5 to 10.")]
    public Task<string> ListCommands(
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Page size from 1 to 100. Use 5 to 10 for quick inspection.")] int pageSize = 10,
        [Description("Optional node id filter.")] Guid? nodeId = null,
        [Description("Optional status filters. Valid values: Received, SttFailed, SttSuccess, LlmFailed, LlmSuccess, TtsFailed, TtsSuccess, HttpFailed, Completed, Failed, Canceled.")] string[]? status = null,
        CancellationToken cancellationToken = default) =>
        Call(() => api.ListCommandsAsync(page, pageSize, nodeId, status, cancellationToken), cancellationToken);

    [McpServerTool(Name = "get_command")]
    [Description("Get one voice command with transcript, response text, timings, status, error, and optionally the full LLM conversation.")]
    public Task<string> GetCommand(
        [Description("Command id (GUID).")] Guid commandId,
        [Description("Include the full LLM conversation JSON. Default false to save context.")] bool includeLlmConversation = false,
        CancellationToken cancellationToken = default) =>
        Call(() => api.GetCommandAsync(commandId, includeLlmConversation, cancellationToken), cancellationToken);

    [McpServerTool(Name = "get_command_tools")]
    [Description("List tool documents changed by one command, with action (created, updated, deleted) and payload.")]
    public Task<string> GetCommandTools(
        [Description("Command id (GUID).")] Guid commandId,
        CancellationToken cancellationToken = default) =>
        Call(() => api.GetCommandToolsAsync(commandId, cancellationToken), cancellationToken);

    [McpServerTool(Name = "get_command_logs")]
    [Description("Fetch Loki log entries correlated with one command. Requires Loki on the server. Large results are truncated.")]
    public Task<string> GetCommandLogs(
        [Description("Command id (GUID).")] Guid commandId,
        [Description("Maximum entries to return, from 1 to 500.")] int maxEntries = 50,
        CancellationToken cancellationToken = default) =>
        Call(() => api.GetCommandLogsAsync(commandId, maxEntries, cancellationToken), cancellationToken);

    [McpServerTool(Name = "list_tool_documents")]
    [Description("List live or archived tool documents such as shopping lists and timers. Supports type, text search, and sorting.")]
    public Task<string> ListToolDocuments(
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Page size from 1 to 100. Use 5 to 10 for quick inspection.")] int pageSize = 10,
        [Description("Document scope: live, archived, or all.")] string scope = "live",
        [Description("Optional document type filter, for example ingredient-list or timer.")] string? type = null,
        [Description("Optional text search over type and payload.")] string? search = null,
        [Description("Sort field: createdAt, type, or id.")] string? sortBy = "createdAt",
        [Description("Sort direction: asc or desc.")] string? sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Call(
            () => api.ListToolDocumentsAsync(
                page,
                pageSize,
                scope,
                type,
                search,
                sortBy,
                sortDirection,
                cancellationToken),
            cancellationToken);

    [McpServerTool(Name = "get_tool_document")]
    [Description("Get one tool document with its payload and command history.")]
    public Task<string> GetToolDocument(
        [Description("Tool document id (GUID).")] Guid documentId,
        CancellationToken cancellationToken = default) =>
        Call(() => api.GetToolDocumentAsync(documentId, cancellationToken), cancellationToken);

    private static async Task<string> Call(
        Func<Task<string>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (StefanApiException exception)
        {
            throw new McpException(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            throw new McpException($"Cannot reach the Stefan API: {exception.Message}");
        }
        catch (JsonException exception)
        {
            throw new McpException($"The Stefan API returned invalid JSON: {exception.Message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new McpException("The Stefan API request timed out.");
        }
    }
}
