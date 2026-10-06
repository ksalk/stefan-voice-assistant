using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace Stefan.Node.Logging;

/// <summary>
/// Builds the node logger from configuration. Settings are read key by key (instead of binding an
/// options POCO or using Serilog.Settings.Configuration) so the logger stays AOT/trimming friendly.
/// </summary>
public static class NodeLogger
{
    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}";

    public static Serilog.ILogger Create(IConfiguration configuration)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: OutputTemplate, standardErrorFromLevel: LogEventLevel.Error);

        loggerConfiguration = ApplyMinimumLevel(loggerConfiguration, configuration);

        if (!bool.TryParse(configuration["Log:File:Enabled"], out var fileEnabled) || fileEnabled)
        {
            var path = configuration["Log:File:Path"] ?? Path.Combine("logs", "stefan-node-.log");
            var retainedFileCountLimit = int.TryParse(configuration["Log:File:RetainedFileCountLimit"], out var retained)
                ? retained
                : 7;

            loggerConfiguration = loggerConfiguration.WriteTo.File(
                path,
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCountLimit,
                shared: true);
        }

        var otlpEndpoint = configuration["Log:Otlp:Endpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            loggerConfiguration = loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = configuration["Log:Otlp:ServiceName"] is { Length: > 0 } serviceName
                        ? serviceName
                        : configuration["Node:Name"] ?? throw new InvalidOperationException("Node should have an unique name."), //TODO: handle it gracefully
                    ["service.namespace"] = "stefan",
                    ["service.version"] = typeof(NodeLogger).Assembly.GetName().Version?.ToString() ?? "unknown",
                    ["deployment.environment"] = ResolveEnvironment(configuration)
                };
                AddHeaders(configuration, options);
            });
        }

        return loggerConfiguration.CreateLogger();
    }

    private static void AddHeaders(IConfiguration configuration, OpenTelemetrySinkOptions options)
    {
        foreach (var header in configuration.GetSection("Log:Otlp:Headers").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(header.Value))
            {
                options.Headers.Add(header.Key, header.Value);
            }
        }
    }

    /// <summary>
    /// Resolves the running environment for the <c>deployment.environment</c> resource attribute.
    /// Explicit config wins, then the standard environment variables; "Production" is the default
    /// for a node deployed without any of them.
    /// </summary>
    private static string ResolveEnvironment(IConfiguration configuration) =>
        configuration["Log:Otlp:Environment"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? "Production";

    private static LoggerConfiguration ApplyMinimumLevel(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        var defaultLevel = ParseLevel(configuration["Log:MinimumLevel:Default"]) ?? LogEventLevel.Information;
        loggerConfiguration = loggerConfiguration.MinimumLevel.Is(defaultLevel);

        foreach (var levelOverride in configuration.GetSection("Log:MinimumLevel:Override").GetChildren())
        {
            if (ParseLevel(levelOverride.Value) is { } level)
            {
                loggerConfiguration = loggerConfiguration.MinimumLevel.Override(levelOverride.Key, level);
            }
        }

        return loggerConfiguration;
    }

    private static LogEventLevel? ParseLevel(string? value) =>
        Enum.TryParse(value, true, out LogEventLevel level) ? level : null;
}
