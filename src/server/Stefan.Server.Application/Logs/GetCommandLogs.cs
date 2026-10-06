using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stefan.Server.Application.Logs;

public class GetCommandLogsRequest
{
    public required Guid CommandId { get; init; }
}

public record LogEntryDto(DateTimeOffset Timestamp, string Line, IReadOnlyDictionary<string, string> Labels);

public class GetCommandLogsResult
{
    public List<LogEntryDto> Entries { get; init; } = [];
}

/// <summary>
/// Fetches log entries related to a single voice command from Loki by correlating them on the
/// command id that the node generated and that flows through both applications' log context.
/// </summary>
public class GetCommandLogs(LokiHttpClient lokiClient, IOptions<LokiOptions> options, ILogger<GetCommandLogs> logger)
{
    public async Task<Result<GetCommandLogsResult>> Handle(GetCommandLogsRequest request, CancellationToken cancellationToken)
    {
        var lokiOptions = options.Value;

        if (string.IsNullOrWhiteSpace(lokiOptions.Url))
        {
            return Result<GetCommandLogsResult>.Failure(
                Error.External("Log backend is not configured."));
        }

        var query = lokiOptions.CommandLogsQueryTemplate.Replace("{CommandId}", request.CommandId.ToString(), StringComparison.Ordinal);

        var end = DateTimeOffset.UtcNow;
        var start = end.AddHours(-lokiOptions.MaxLookbackHours);

        logger.LogDebug(
            "Querying Loki for logs of command {CommandId} with query {Query} between {Start} and {End}",
            request.CommandId, query, start, end);

        try
        {
            var entries = await lokiClient.QueryRangeAsync(
                query,
                ToUnixTimeNanoseconds(start),
                ToUnixTimeNanoseconds(end),
                lokiOptions.Limit,
                cancellationToken);

            return new GetCommandLogsResult
            {
                Entries = entries
                    .Select(e => new LogEntryDto(e.Timestamp, e.Line, e.Labels))
                    .ToList(),
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Loki query for command {CommandId} failed", request.CommandId);
            return Result<GetCommandLogsResult>.Failure(Error.External("Log query failed: the log backend is unreachable."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Loki query for command {CommandId} returned an unexpected response", request.CommandId);
            return Result<GetCommandLogsResult>.Failure(Error.External("Log query failed: unexpected log backend response."));
        }
    }

    private static long ToUnixTimeNanoseconds(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds() * 1_000_000;
}
