using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stefan.Mcp;
using Stefan.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the MCP stdio protocol. Send every log to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

var apiUrl = builder.Configuration["STEFAN_API_URL"];
if (string.IsNullOrWhiteSpace(apiUrl))
{
    Console.Error.WriteLine(
        "STEFAN_API_URL is required. Set it in .env.mcp or export it, for example http://stefan.example:5285.");
    return 1;
}

builder.Services.AddHttpClient<StefanApiClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();

    client.BaseAddress = StefanApiClient.CreateBaseAddress(apiUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("stefan-mcp/1.0");

    var token = configuration["STEFAN_API_TOKEN"];
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<StefanTools>();

await builder.Build().RunAsync();
return 0;
