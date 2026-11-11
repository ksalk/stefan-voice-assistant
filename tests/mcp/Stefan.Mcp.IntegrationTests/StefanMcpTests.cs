using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Stefan.Mcp.IntegrationTests;

public class StefanMcpTests
{
    private static readonly string[] ExpectedTools =
    [
        "get_command",
        "get_command_logs",
        "get_command_tools",
        "get_node",
        "get_tool_document",
        "health",
        "list_commands",
        "list_nodes",
        "list_tool_documents"
    ];

    [Theory]
    [InlineData("http://stefan.example:5285", "http://stefan.example:5285/api/")]
    [InlineData("http://stefan.example:5285/", "http://stefan.example:5285/api/")]
    [InlineData("http://stefan.example:5285/api", "http://stefan.example:5285/api/")]
    [InlineData("http://stefan.example:5285/api/nodes", "http://stefan.example:5285/api/")]
    [InlineData("http://stefan.example:5285/stefan/api", "http://stefan.example:5285/stefan/api/")]
    public void Base_address_accepts_origin_and_endpoint_urls(string input, string expected)
    {
        Assert.Equal(new Uri(expected), StefanApiClient.CreateBaseAddress(input));
    }

    [Fact]
    public async Task Tools_list_exposes_only_read_only_tools()
    {
        await using var api = await FakeStefanApi.StartAsync();
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var tools = await client.ListToolsAsync();

        var names = tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(ExpectedTools, names);
    }

    [Fact]
    public async Task List_commands_forwards_pagination_node_and_status_filters()
    {
        await using var api = await FakeStefanApi.StartAsync();
        api.Handler = context =>
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("""{"items":[],"totalCount":0,"page":1,"pageSize":5}""");
        };
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var nodeId = Guid.NewGuid();
        var result = await client.CallToolAsync(
            "list_commands",
            new Dictionary<string, object?>
            {
                ["page"] = 1,
                ["pageSize"] = 5,
                ["nodeId"] = nodeId.ToString(),
                ["status"] = new[] { "Completed", "Failed" }
            });

        Assert.Contains("\"items\":[]", Text(result));

        var request = Assert.Single(api.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/commands", request.Path);
        Assert.Contains("page=1", request.Query);
        Assert.Contains("pageSize=5", request.Query);
        Assert.Contains($"nodeId={nodeId:D}", request.Query);
        Assert.Contains("status=Completed", request.Query);
        Assert.Contains("status=Failed", request.Query);
    }

    [Fact]
    public async Task Get_command_logs_truncates_entries()
    {
        await using var api = await FakeStefanApi.StartAsync();
        api.Handler = context =>
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("""{"entries":[{"line":"one"},{"line":"two"},{"line":"three"}]}""");
        };
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var commandId = Guid.NewGuid();
        var result = await client.CallToolAsync(
            "get_command_logs",
            new Dictionary<string, object?>
            {
                ["commandId"] = commandId.ToString(),
                ["maxEntries"] = 2
            });

        using var document = JsonDocument.Parse(Text(result));
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("returnedEntries").GetInt32());
        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(2, root.GetProperty("entries").GetArrayLength());
        Assert.Equal($"/api/commands/{commandId:D}/logs", Assert.Single(api.Requests).Path);
    }

    [Fact]
    public async Task Get_node_trims_status_reports()
    {
        await using var api = await FakeStefanApi.StartAsync();
        api.Handler = context =>
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(
                """
                {
                  "node": {
                    "id": "11111111-1111-1111-1111-111111111111",
                    "name": "test",
                    "statusReports": [
                      { "timestamp": "2026-01-03T00:00:00Z" },
                      { "timestamp": "2026-01-02T00:00:00Z" },
                      { "timestamp": "2026-01-01T00:00:00Z" }
                    ]
                  }
                }
                """);
        };
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var nodeId = Guid.NewGuid();
        var result = await client.CallToolAsync(
            "get_node",
            new Dictionary<string, object?>
            {
                ["nodeId"] = nodeId.ToString(),
                ["maxStatusReports"] = 2
            });

        using var document = JsonDocument.Parse(Text(result));
        var node = document.RootElement.GetProperty("node");

        Assert.Equal(2, node.GetProperty("statusReports").GetArrayLength());
        Assert.Equal(2, node.GetProperty("returnedStatusReports").GetInt32());
        Assert.Equal(3, node.GetProperty("totalStatusReports").GetInt32());
        Assert.True(node.GetProperty("statusReportsTruncated").GetBoolean());
    }

    [Fact]
    public async Task Get_command_strips_llm_conversation_unless_requested()
    {
        await using var api = await FakeStefanApi.StartAsync();
        api.Handler = context =>
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(
                """{"id":"11111111-1111-1111-1111-111111111111","llmConversationJson":"[{\"role\":\"user\"}]"}""");
        };
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var commandId = Guid.NewGuid();
        var arguments = new Dictionary<string, object?>
        {
            ["commandId"] = commandId.ToString()
        };

        var withoutConversation = await client.CallToolAsync("get_command", arguments);
        using (var document = JsonDocument.Parse(Text(withoutConversation)))
        {
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("llmConversationJson").ValueKind);
        }

        var withConversation = await client.CallToolAsync(
            "get_command",
            new Dictionary<string, object?>
            {
                ["commandId"] = commandId.ToString(),
                ["includeLlmConversation"] = true
            });
        using (var document = JsonDocument.Parse(Text(withConversation)))
        {
            Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("llmConversationJson").ValueKind);
        }
    }

    [Fact]
    public async Task Upstream_error_is_returned_as_tool_error()
    {
        await using var api = await FakeStefanApi.StartAsync();
        api.Handler = context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return context.Response.WriteAsync("boom");
        };
        await using var client = await CreateMcpClientAsync(api.BaseUrl);

        var result = await client.CallToolAsync("health", new Dictionary<string, object?>());

        Assert.True(result.IsError);
        var text = Text(result);
        Assert.Contains("500", text);
        Assert.Contains("boom", text);
        Assert.DoesNotContain("   at ", text);
    }

    private static async Task<McpClient> CreateMcpClientAsync(string apiUrl)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "stefan-mcp-test",
            Command = "dotnet",
            Arguments = [McpServerPath()],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["STEFAN_API_URL"] = apiUrl
            }
        });

        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().First().Text;

    private static string McpServerPath()
    {
        var root = FindRepositoryRoot();
        var configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        var path = Path.Combine(
            root,
            "src",
            "mcp",
            "Stefan.Mcp",
            "bin",
            configuration,
            "net10.0",
            "Stefan.Mcp.dll");

        Assert.True(File.Exists(path), $"MCP server assembly not found at {path}");
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Stefan.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}

internal sealed class FakeStefanApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeStefanApi(WebApplication app) => _app = app;

    public string BaseUrl { get; private set; } = string.Empty;

    public List<RecordedRequest> Requests { get; } = [];

    public Func<HttpContext, Task>? Handler { get; set; }

    public static async Task<FakeStefanApi> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var fake = new FakeStefanApi(app);

        app.Run(async context =>
        {
            fake.Requests.Add(new RecordedRequest(
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Request.QueryString.Value ?? string.Empty));

            if (fake.Handler is not null)
            {
                await fake.Handler(context);
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"ok":true}""");
        });

        await app.StartAsync();
        fake.BaseUrl = app.Urls.First();
        return fake;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

internal sealed record RecordedRequest(string Method, string Path, string Query);
