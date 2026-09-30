using System.Reflection;
using Microsoft.Extensions.Options;
using Stefan.Server.Application.AI;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Services;
using Stefan.Server.Application.Tools;

namespace Stefan.Server.API;

public class Startup
{
    /// <summary>
    /// Runs the custom startup tasks in order: reschedules node ping jobs, eagerly loads
    /// the STT/TTS models, and logs the effective configuration.
    /// </summary>
    public static async Task RunStartupTasksAsync(WebApplication app)
    {
        // Reschedule ping jobs for all online nodes after server restart
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var rescheduleNodePings = services.GetRequiredService<RescheduleNodePings>();
            await rescheduleNodePings.Handle(CancellationToken.None);
        }

        // Eagerly load the STT model so it's ready before the first request.
        app.Services.GetRequiredService<ISpeechToTextService>();

        // Eagerly load the TTS engine (downloads piper/model if missing) so it's ready before the first request.
        app.Services.GetRequiredService<ITextToSpeechService>();

        LogStartupInfo(app);
    }

    /// <summary>
    /// Logs the effective startup configuration (version, environment, STT/TTS/LLM providers,
    /// registered tools, DB host, CORS and OTLP status) to help debugging misconfiguration.
    /// Secrets (API keys, connection string contents) are never logged.
    /// </summary>
    private static void LogStartupInfo(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Startup>>();
        var configuration = app.Services.GetRequiredService<IConfiguration>();

        var informationalVersion = typeof(Startup).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var parts = informationalVersion.Split('+', 2);
        var version = parts[0];
        var commitHash = parts.Length > 1 ? parts[1] : "unknown";

        logger.LogInformation(
            "Stefan server starting {Version} (commit {CommitHash}), environment {Environment}, content root {ContentRoot}",
            version, commitHash, app.Environment.EnvironmentName, app.Environment.ContentRootPath);

        var sttProvider = configuration["SttProvider"] ?? "Whisper";
        logger.LogInformation("STT provider: {SttProvider} ({SttDetail})", sttProvider, DescribeSttProvider(configuration, sttProvider));

        var ttsProvider = configuration["TtsProvider"] ?? "Piper";
        logger.LogInformation("TTS provider: {TtsProvider} ({TtsDetail})", ttsProvider, DescribeTtsProvider(configuration, ttsProvider));

        var openAiOptions = app.Services.GetRequiredService<IOptions<OpenAiOptions>>().Value;
        logger.LogInformation(
            "LLM: model {LlmModel} at {LlmEndpoint}",
            string.IsNullOrWhiteSpace(openAiOptions.Model) ? "NOT CONFIGURED" : openAiOptions.Model,
            string.IsNullOrWhiteSpace(openAiOptions.Endpoint) ? "NOT CONFIGURED" : openAiOptions.Endpoint);

        using (var scope = app.Services.CreateScope())
        {
            var toolNames = scope.ServiceProvider
                .GetRequiredService<ToolRegistry>()
                .GetAllToolDefinitions()
                .Select(t => t.FunctionName)
                .ToList();
            logger.LogInformation("Registered LLM tools ({ToolCount}): {Tools}", toolNames.Count, string.Join(", ", toolNames));
        }

        var otlpEndpoint = configuration["Log:Otlp:Endpoint"];
        logger.LogInformation(
            "OTLP log sink: {OtlpStatus}",
            string.IsNullOrWhiteSpace(otlpEndpoint) ? "disabled" : otlpEndpoint);

        var connectionString = configuration.GetConnectionString("StefanDb");
        logger.LogInformation("Database host: {DbHost}", DescribeDbHost(connectionString));

        logger.LogInformation("Dashboard CORS allowed origins: {CorsOrigins}",
            configuration["Cors:Dashboard:AllowedOrigins"] is { Length: > 0 } origins ? origins : "NOT CONFIGURED");
    }

    /// <summary>
    /// Builds a human-readable detail string for the configured STT provider.
    /// </summary>
    private static string DescribeSttProvider(IConfiguration configuration, string sttProvider)
    {
        if (sttProvider.Equals("Vosk", StringComparison.OrdinalIgnoreCase))
        {
            return $"model path '{configuration["Vosk:ModelPath"] ?? "../../stt-models/vosk-model-en-us-0.22"}'";
        }

        if (sttProvider.Equals("XAi", StringComparison.OrdinalIgnoreCase))
        {
            return $"endpoint '{configuration["xAI:Endpoint"] ?? "https://api.x.ai/v1"}', " +
                   $"language '{configuration["xAI:Language"] ?? "en"}'";
        }

        return $"model path '{configuration["Whisper:ModelPath"] ?? "ggml-base.bin"}', language 'en'";
    }

    /// <summary>
    /// Builds a human-readable detail string for the configured TTS provider.
    /// </summary>
    private static string DescribeTtsProvider(IConfiguration configuration, string ttsProvider)
    {
        if (ttsProvider.Equals("XAi", StringComparison.OrdinalIgnoreCase))
        {
            return $"endpoint '{configuration["xAI:Endpoint"] ?? "https://api.x.ai/v1"}', " +
                   $"voice '{configuration["xAI:TtsVoiceId"] ?? "leo"}', " +
                   $"language '{configuration["xAI:Language"] ?? "en"}'";
        }

        var piper = configuration.GetSection("Piper");
        return $"model key '{piper["ModelKey"] ?? "en_US-hfc_female-medium"}', " +
               $"executable '{piper["ExecutablePath"] ?? "piper/piper"}'";
    }

    /// <summary>
    /// Renders the host from a PostgreSQL connection string.
    /// </summary>
    private static string DescribeDbHost(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "NOT CONFIGURED";
        }

        static string? Value(string source, string key)
        {
            return source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pair => pair.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                .Select(parts => parts[1])
                .FirstOrDefault();
        }

        return Value(connectionString, "Host") ?? Value(connectionString, "Server") ?? "unknown";
    }
}
