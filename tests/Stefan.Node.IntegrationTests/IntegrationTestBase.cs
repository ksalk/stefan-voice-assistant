using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Stefan.Node.IntegrationTests;

public enum ContainerStartMode
{
    ExpectRunning,
    ExpectExit,
}

public abstract class IntegrationTestBase
{
    private const string ImageName = "stefan-node:test";
    private const string AuthSecret = "test-secret";

    protected async Task<NodeApp> CreateNodeApp(
        ContainerStartMode startMode = ContainerStartMode.ExpectRunning,
        Func<ContainerBuilder, ContainerBuilder>? configureContainer = null,
        Action<WebApplication>? configureServer = null)
    {
        var testRunId = Guid.NewGuid().ToString("D");
        var pipeDirectory = Path.Combine("audio-pipes", testRunId);
        var pipePath = Path.Combine(pipeDirectory, "audio-input");
        Directory.CreateDirectory(pipeDirectory);
        File.WriteAllText(pipePath, string.Empty);

        var serverBuilder = WebApplication.CreateBuilder();
        serverBuilder.Logging.ClearProviders();
        serverBuilder.WebHost.UseUrls("http://0.0.0.0:0");
        var mockServer = serverBuilder.Build();

        if (configureServer is not null)
            configureServer(mockServer);
        else
            mockServer.MapPost("/api/nodes/register", () => Results.Ok());

        await mockServer.StartAsync();

        var urls = mockServer.Urls.ToArray();
        var url = urls.First(u => u.StartsWith("http://"));
        var mockServerPort = int.Parse(url.Split(':')[2]);

        var hostPipeDir = Path.GetFullPath($"./audio-pipes/{testRunId}");

        var containerBuilder = new ContainerBuilder(ImageName)
            // Enable to show logs from container
            //.WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
            .WithName($"stefan-node-integration-test-{testRunId}")
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("Node__Name", "stefan-node-test")
            .WithEnvironment("Server__Url", "http://0.0.0.0:8080")
            .WithEnvironment("RemoteServer__Url", $"http://host.docker.internal:{mockServerPort}")
            .WithEnvironment("RemoteServer__AuthSecret", AuthSecret)
            .WithEnvironment("Audio__InputSource", "pipe")
            .WithEnvironment("Audio__PipePath", $"/tmp/audio-pipes/{testRunId}/audio-input")
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
            .WithBindMount(hostPipeDir, $"/tmp/audio-pipes/{testRunId}")
            .WithWaitStrategy(BuildWaitStrategy(startMode));

        if (configureContainer is not null)
            containerBuilder = configureContainer(containerBuilder);

        var container = containerBuilder.Build();
        await container.StartAsync();

        var port = container.GetMappedPublicPort(8080);
        var host = container.Hostname;

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{port}"),
            Timeout = TimeSpan.FromSeconds(5),
        };

        return new NodeApp(httpClient, pipePath, pipeDirectory, mockServerPort, container, mockServer);
    }

    private static IWaitForContainerOS BuildWaitStrategy(ContainerStartMode startMode)
    {
        var baseStrategy = Wait.ForUnixContainer();

        return startMode switch
        {
            ContainerStartMode.ExpectRunning => baseStrategy
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(8080)),

            ContainerStartMode.ExpectExit => baseStrategy
                .AddCustomWaitStrategy(new ExitWaitUntil(TimeSpan.FromSeconds(30))),

            _ => throw new ArgumentOutOfRangeException(nameof(startMode)),
        };
    }

    private class ExitWaitUntil(TimeSpan timeout) : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            using var cts = new CancellationTokenSource(timeout);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var exitCode = await container.GetExitCodeAsync(cts.Token);
                    return exitCode != -1;
                }
                catch
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
                }
            }

            throw new TimeoutException("Container did not exit within the timeout period.");
        }
    }

    public class NodeApp : IAsyncDisposable
    {
        private readonly IContainer _container;
        private readonly WebApplication _mockServer;
        private readonly string _pipeDirectory;
        private readonly string _pipePath;

        public NodeApp(
            HttpClient httpClient,
            string pipePath,
            string pipeDirectory,
            int mockServerPort,
            IContainer container,
            WebApplication mockServer)
        {
            HttpClient = httpClient;
            MockServerPort = mockServerPort;
            _container = container;
            _mockServer = mockServer;
            _pipeDirectory = pipeDirectory;
            _pipePath = pipePath;
        }

        public HttpClient HttpClient { get; }
        public int MockServerPort { get; }

        public async Task<long> GetExitCodeAsync(CancellationToken cancellationToken = default) =>
            await _container.GetExitCodeAsync(cancellationToken);

        public async Task<(string Stdout, string Stderr)> GetLogsAsync(CancellationToken cancellationToken = default) =>
            await _container.GetLogsAsync(DateTime.MinValue, DateTime.MaxValue, false, cancellationToken);

        public async Task WriteAudioAsync(byte[] data, CancellationToken cancellationToken = default) =>
            await File.WriteAllBytesAsync(_pipePath, data, cancellationToken);

        public async Task WriteSilenceAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            const int sampleRate = 16000;
            const int channels = 2;
            const int bytesPerSample = 2;
            var bytesPerSecond = sampleRate * channels * bytesPerSample;
            var byteCount = (int)(bytesPerSecond * duration.TotalSeconds);
            await File.WriteAllBytesAsync(_pipePath, new byte[byteCount], cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _container.DisposeAsync();
            await _mockServer.StopAsync();
            if (Directory.Exists(_pipeDirectory))
                Directory.Delete(_pipeDirectory, true);
            HttpClient.Dispose();
        }
    }
}
