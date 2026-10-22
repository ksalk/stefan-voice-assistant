namespace Stefan.Server.Application.Logs;

public class LokiOptions
{
    public const string SectionName = "Loki";

    /// <summary>Base URL of the Loki instance. When empty the logs feature is disabled.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// LogQL template used to correlate logs to a command. The <c>CommandId</c> placeholder is
    /// replaced with the command id before the query is sent.
    /// </summary>
    public string CommandLogsQueryTemplate { get; set; } = "{service_namespace=\"stefan\", deployment_environment=\"{Environment}\"} | json | attributes_CommandId=\"{CommandId}\"";

    /// <summary>
    /// How much time before and after the command the query covers, in minutes. The window
    /// follows the command, so the logs of old commands stay available.
    /// </summary>
    public int CommandWindowPaddingMinutes { get; set; } = 15;

    /// <summary>
    /// How far back from now the query covers when the command is not in the database, in hours.
    /// Keep it below the query length limit of the log backend.
    /// </summary>
    public int FallbackLookbackHours { get; set; } = 720;

    /// <summary>Maximum number of log entries returned by a single query.</summary>
    public int Limit { get; set; } = 1000;

    /// <summary>Optional request headers, e.g. authentication credentials for the Loki endpoint.</summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;
}
