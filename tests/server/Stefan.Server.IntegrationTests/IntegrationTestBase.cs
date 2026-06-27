using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Stefan.Server.IntegrationTests;

public abstract class IntegrationTestBase
{
    private const string ServerImageName = "stefan-server:test";
    private const string DbImageName = "stefan-db:test";

    private const string NetworkAlias = "stefan-db-test";
    private const string DbUser = "stefan";
    private const string DbPassword = "changeme";
    private const string DbName = "stefan_db";

    protected async Task<ServerApp> CreateServerApp(
        Func<ContainerBuilder, ContainerBuilder>? configureContainer = null)
    {
        var testRunId = Guid.NewGuid().ToString("D");

        var network = new NetworkBuilder()
            .WithName($"stefan-server-test-{testRunId}")
            .Build();
        await network.CreateAsync();

        var db = new PostgreSqlBuilder(DbImageName)
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithDatabase(DbName)
            .WithUsername(DbUser)
            .WithPassword(DbPassword)
            .Build();
        await db.StartAsync();

        var connectionString =
            $"Host={NetworkAlias};Port=5432;Database={DbName};Username={DbUser};Password={DbPassword}";

        var containerBuilder = new ContainerBuilder(ServerImageName)
            .WithName($"stefan-server-test-{testRunId}")
            .WithNetwork(network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("ConnectionStrings__StefanDb", connectionString)
            .WithEnvironment("Cors__Dashboard__AllowedOrigins", "http://localhost")
            .WithEnvironment("NodeSecret", "test-secret")
            .WithEnvironment("OpenAI__ApiKey", "test-dummy")
            .WithEnvironment("SttProvider", "XAi")
            .WithEnvironment("TtsProvider", "XAi")
            .WithEnvironment("xAI__ApiKey", "test-dummy")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/api/health").ForPort(8080)));

        if (configureContainer is not null)
            containerBuilder = configureContainer(containerBuilder);

        var serverContainer = containerBuilder.Build();
        await serverContainer.StartAsync();

        var port = serverContainer.GetMappedPublicPort(8080);
        var host = serverContainer.Hostname;

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{port}"),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var client = new ServerAppClient(httpClient);

        var hostConnectionString = db.GetConnectionString();

        return new ServerApp(client, db, serverContainer, network, hostConnectionString);
    }

    public sealed class ServerApp : IAsyncDisposable
    {
        public ServerApp(
            ServerAppClient client,
            PostgreSqlContainer dbContainer,
            IContainer serverContainer,
            INetwork network,
            string dbConnectionString)
        {
            Client = client;
            DbContainer = dbContainer;
            ServerContainer = serverContainer;
            Network = network;
            DbConnectionString = dbConnectionString;
        }

        public ServerAppClient Client { get; }
        public PostgreSqlContainer DbContainer { get; }
        public IContainer ServerContainer { get; }
        public INetwork Network { get; }
        public string DbConnectionString { get; }

        public async Task<(string Stdout, string Stderr)> GetLogsAsync(CancellationToken cancellationToken = default) =>
            await ServerContainer.GetLogsAsync(DateTime.MinValue, DateTime.MaxValue, false, cancellationToken);

        public async Task<T> QueryScalarAsync<T>(string sql, IReadOnlyDictionary<string, object?>? args = null)
        {
            await using var conn = new NpgsqlConnection(DbConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            if (args is not null)
                foreach (var (name, value) in args)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return (T)result!;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await ServerContainer.DisposeAsync();
            await DbContainer.DisposeAsync();
            await Network.DeleteAsync();
        }
    }
}