using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Logs;

public class GetCommandLogsRequest
{
    public required Guid CommandId { get; init; }

    /// <summary>Environment of the fetching server; used to select the matching {Environment} filter in the query template.</summary>
    public required string Environment { get; init; }
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
public class GetCommandLogs(
    LokiHttpClient lokiClient,
    StefanDbContext dbContext,
    IOptions<LokiOptions> options,
    ILogger<GetCommandLogs> logger)
{
    public async Task<Result<GetCommandLogsResult>> Handle(GetCommandLogsRequest request, CancellationToken cancellationToken)
    {
        var lokiOptions = options.Value;

        if (string.IsNullOrWhiteSpace(lokiOptions.Url))
        {
            return Result<GetCommandLogsResult>.Failure(
                Error.External("Log backend is not configured."));
        }

        var query = lokiOptions.CommandLogsQueryTemplate
            .Replace("{CommandId}", request.CommandId.ToString(), StringComparison.Ordinal)
            .Replace("{Environment}", request.Environment, StringComparison.Ordinal);

        var (start, end) = await ResolveWindowAsync(request.CommandId, lokiOptions, cancellationToken);

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

    /// <summary>
    /// Resolves the queried time range. The range follows the command, so the logs of a command
    /// stay available no matter how old the command is. Commands that are not in the database
    /// fall back to a range that ends now.
    /// </summary>
    private async Task<(DateTimeOffset Start, DateTimeOffset End)> ResolveWindowAsync(
        Guid commandId,
        LokiOptions lokiOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = await dbContext.CommandRecords
                .AsNoTracking()
                .Where(r => r.Id == commandId)
                .Select(r => new { r.ReceivedAt, r.TotalDurationMs })
                .FirstOrDefaultAsync(cancellationToken);

            if (command is not null)
            {
                var receivedAt = new DateTimeOffset(DateTime.SpecifyKind(command.ReceivedAt, DateTimeKind.Utc));
                var padding = TimeSpan.FromMinutes(lokiOptions.CommandWindowPaddingMinutes);
                var duration = TimeSpan.FromMilliseconds(command.TotalDurationMs ?? 0);

                return (receivedAt - padding, receivedAt + duration + padding);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not read the timestamp of command {CommandId}. Falling back to the default lookback.",
                commandId);
        }

        var end = DateTimeOffset.UtcNow;
        return (end.AddHours(-lokiOptions.FallbackLookbackHours), end);
    }

    private static long ToUnixTimeNanoseconds(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds() * 1_000_000;
}
