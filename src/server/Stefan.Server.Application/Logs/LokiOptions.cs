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
    public string CommandLogsQueryTemplate { get; set; } = "{service_namespace=\"stefan\"} | json | attributes_CommandId=\"{CommandId}\"";

    /// <summary>How far back from now the query covers.</summary>
    public int MaxLookbackHours { get; set; } = 24;

    /// <summary>Maximum number of log entries returned by a single query.</summary>
    public int Limit { get; set; } = 1000;

    /// <summary>Optional request headers, e.g. authentication credentials for the Loki endpoint.</summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;
}
