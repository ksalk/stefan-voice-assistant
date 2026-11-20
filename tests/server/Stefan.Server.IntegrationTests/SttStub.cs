using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Stefan.Server.IntegrationTests;

/// <summary>A fake xAI STT endpoint that returns an empty transcript.</summary>
public sealed class SttStub : IAsyncDisposable
{
    public const string Image = "nginx:alpine";

    /// <summary>Network alias under which the stub answers.</summary>
    public const string NetworkAlias = "stt";

    /// <summary>Base URL the server under test uses to reach the stub.</summary>
    public const string Url = "http://stt/v1";

    private const string Config = """
        server {
            listen 80;
            location / {
                default_type application/json;
                return 200 '{"text":""}';
            }
        }
        """;

    private readonly IContainer _container;

    private SttStub(IContainer container) => _container = container;

    public static async Task<SttStub> StartAsync(INetwork network)
    {
        var container = new ContainerBuilder(Image)
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Config), "/etc/nginx/conf.d/default.conf")
            .WithPortBinding(80, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/").ForPort(80)))
            .WithLogger(NullLogger.Instance)
            .Build();

        await container.StartAsync();
        return new SttStub(container);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
