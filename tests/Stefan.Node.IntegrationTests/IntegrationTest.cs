using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Stefan.Node.IntegrationTests;

public class IntegrationTest : IAsyncLifetime
{
    private readonly string _pipeId = Guid.NewGuid().ToString("D");
    private string _pipeDirectory = null!;
    private string _pipePath = null!;
    private WebApplication _mockServer = null!;
    private IContainer _stefanNodeContainer = null!;
    private int _mockServerPort;
    private const string ImageName = "stefan-node:test";
    private const string AuthSecret = "test-secret";

    public async Task InitializeAsync()
    {
        _pipeDirectory = Path.Combine("audio-pipes", _pipeId);
        _pipePath = Path.Combine(_pipeDirectory, "audio-input");
        Directory.CreateDirectory(_pipeDirectory);
        File.WriteAllText(_pipePath, string.Empty);

        _mockServer = CreateMockServer();
        await _mockServer.StartAsync();

        var urls = _mockServer.Urls.ToArray();
        var url = urls.First(u => u.StartsWith("http://"));
        _mockServerPort = int.Parse(url.Split(':')[2]);

        var hostPipeDir = Path.GetFullPath($"./audio-pipes/{_pipeId}");

        _stefanNodeContainer = new ContainerBuilder(ImageName)
            .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("Node__Name", "stefan-node-test")
            .WithEnvironment("Server__Url", "http://0.0.0.0:8080")
            .WithEnvironment("RemoteServer__Url", $"http://host.docker.internal:{_mockServerPort}")
            .WithEnvironment("RemoteServer__AuthSecret", AuthSecret)
            .WithEnvironment("Audio__InputSource", "pipe")
            .WithEnvironment("Audio__PipePath", $"/tmp/audio-pipes/{_pipeId}/audio-input")
            .WithEnvironment("Audio__Output__DeviceName", "null")
            .WithEnvironment("Audio__SilenceThreshold", "0.02")
            .WithEnvironment("Audio__SilenceTimeoutMs", "1000")
            .WithEnvironment("Audio__MaxRecordingMs", "10000")
            .WithEnvironment("Audio__Input__SampleRate", "16000")
            .WithEnvironment("Audio__Input__Channels", "2")
            .WithEnvironment("Audio__Input__BitsPerSample", "16")
            .WithEnvironment("KeywordSpotter__NumThreads", "2")
            .WithEnvironment("KeywordSpotter__Provider", "cpu")
            .WithEnvironment("KeywordSpotter__FeatureDim", "80")
            .WithPortBinding(8080, true)
            .WithBindMount(hostPipeDir, $"/tmp/audio-pipes/{_pipeId}")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(8080)))
            .Build();

        await _stefanNodeContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_stefanNodeContainer != null)
            await _stefanNodeContainer.DisposeAsync();

        if (_mockServer != null)
            await _mockServer.StopAsync();

        if (Directory.Exists(_pipeDirectory))
            Directory.Delete(_pipeDirectory, true);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var port = _stefanNodeContainer.GetMappedPublicPort(8080);
        var host = _stefanNodeContainer.Hostname;

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{port}"),
            Timeout = TimeSpan.FromSeconds(5),
        };

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk_AfterSendingSilence()
    {
        var port = _stefanNodeContainer.GetMappedPublicPort(8080);
        var host = _stefanNodeContainer.Hostname;

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{port}"),
            Timeout = TimeSpan.FromSeconds(5),
        };

        var silence = new byte[4096];
        await File.WriteAllBytesAsync(_pipePath, silence);

        await Task.Delay(500);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplication CreateMockServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:0");
        var app = builder.Build();

        app.MapPost("/api/nodes/register", () => Results.Ok());

        return app;
    }
}
