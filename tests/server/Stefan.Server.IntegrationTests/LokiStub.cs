using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Stefan.Server.IntegrationTests;

/// <summary>
/// A fake Loki that records the queries it receives. It answers every request with a canned
/// Loki response and writes the requested URI to its log, so a test can assert on the queried
/// time range.
/// </summary>
public sealed class LokiStub : IAsyncDisposable
{
    public const string Image = "nginx:alpine";

    /// <summary>Network alias under which the stub answers.</summary>
    public const string NetworkAlias = "loki";

    /// <summary>Base URL the server under test uses to reach the stub.</summary>
    public const string Url = "http://loki/";

    private const string RequestMarker = "LOKI_REQUEST ";

    private const string Config = """
        log_format loki 'LOKI_REQUEST $request_uri';
        server {
            listen 80;
            access_log /dev/stdout loki;
            error_log /dev/stderr;
            location / {
                default_type application/json;
                return 200 '{"status":"success","data":{"resultType":"streams","result":[{"stream":{"service_name":"stefan-server"},"values":[["1792494671943752400","stub log line"]]}]}}';
            }
        }
        """;

    private readonly IContainer _container;

    private LokiStub(IContainer container) => _container = container;

    public static async Task<LokiStub> StartAsync(INetwork network)
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
        return new LokiStub(container);
    }

    /// <summary>Returns the URI of the last query the stub received.</summary>
    public async Task<string> GetLastRequestUriAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var (stdout, _) = await _container.GetLogsAsync(DateTime.MinValue, DateTime.MaxValue, false, cancellationToken);

            var requestUri = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains(RequestMarker, StringComparison.Ordinal))
                .Select(line => line[(line.IndexOf(RequestMarker, StringComparison.Ordinal) + RequestMarker.Length)..].Trim())
                .LastOrDefault(line => line.StartsWith('/'));

            if (requestUri is not null)
            {
                return requestUri;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new InvalidOperationException("The Loki stub did not record a query.");
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
