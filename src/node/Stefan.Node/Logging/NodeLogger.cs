using Serilog;
using Serilog.Events;

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

        return loggerConfiguration.CreateLogger();
    }

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
