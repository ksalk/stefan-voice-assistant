using System.Text.Json;
using Npgsql;
using Stefan.Server.Domain;

namespace Stefan.Server.IntegrationTests;

public class CommandLogsTests : IntegrationTestBase
{
    private const int CommandWindowPaddingMinutes = 15;
    private const int FallbackLookbackHours = 720;

    /// <summary>Tolerance of the asserted time range, in nanoseconds.</summary>
    private const long OneMillisecond = 1_000_000;

    [Fact]
    public async Task GetCommandLogs_QueriesWindowAroundTheCommandReceivedAt()
    {
        await using var app = await CreateServerApp(container => container.WithEnvironment("Loki__Url", LokiStub.Url));
        await using var loki = await LokiStub.StartAsync(app.Network);

        var commandId = Guid.NewGuid();
        var receivedAt = DateTime.UtcNow.AddDays(-5);
        const double totalDurationMs = 3200;

        await InsertCommandAsync(app.DbConnectionString, commandId, receivedAt, totalDurationMs);

        var response = await app.Client.GetCommandLogsAsync(commandId);
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var entries = body.RootElement.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("stub log line", entries[0].GetProperty("line").GetString());

        var query = ParseQuery(await loki.GetLastRequestUriAsync());

        Assert.Contains(commandId.ToString(), query["query"], StringComparison.Ordinal);

        // The window follows the command, not the current time.
        var expectedStart = ToUnixTimeNanoseconds(receivedAt.AddMinutes(-CommandWindowPaddingMinutes));
        var expectedEnd = ToUnixTimeNanoseconds(
            receivedAt.AddMilliseconds(totalDurationMs).AddMinutes(CommandWindowPaddingMinutes));

        Assert.InRange(long.Parse(query["start"]), expectedStart - OneMillisecond, expectedStart + OneMillisecond);
        Assert.InRange(long.Parse(query["end"]), expectedEnd - OneMillisecond, expectedEnd + OneMillisecond);
    }

    [Fact]
    public async Task GetCommandLogs_UsesFallbackWindow_WhenCommandIsUnknown()
    {
        await using var app = await CreateServerApp(container => container.WithEnvironment("Loki__Url", LokiStub.Url));
        await using var loki = await LokiStub.StartAsync(app.Network);

        var response = await app.Client.GetCommandLogsAsync(Guid.NewGuid());
        response.EnsureSuccessStatusCode();

        var query = ParseQuery(await loki.GetLastRequestUriAsync());

        var now = DateTimeOffset.UtcNow;
        var expectedEnd = ToUnixTimeNanoseconds(now);
        var expectedStart = ToUnixTimeNanoseconds(now.AddHours(-FallbackLookbackHours));

        // The server reads the current time a moment before the test does.
        const long tolerance = 60 * OneMillisecond;

        Assert.InRange(long.Parse(query["end"]), expectedEnd - tolerance, expectedEnd + tolerance);
        Assert.InRange(long.Parse(query["start"]), expectedStart - tolerance, expectedStart + tolerance);
    }

    private static async Task InsertCommandAsync(
        string connectionString,
        Guid commandId,
        DateTime receivedAt,
        double totalDurationMs)
    {
        const string sql = """
            INSERT INTO "CommandRecords"
                ("Id", "NodeId", "SessionId", "ReceivedAt", "Status", "InputAudio", "InputAudioFormat", "InputAudioDurationMs", "TotalDurationMs")
            VALUES
                (@id, @nodeId, 'test-session', @receivedAt, @status, @inputAudio, 'wav', 0, @totalDurationMs)
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", commandId);
        command.Parameters.AddWithValue("nodeId", Guid.NewGuid());
        command.Parameters.AddWithValue("receivedAt", receivedAt);
        command.Parameters.AddWithValue("status", (int)CommandStatus.TtsSuccess);
        command.Parameters.AddWithValue("inputAudio", new byte[] { 1, 2, 3 });
        command.Parameters.AddWithValue("totalDurationMs", totalDurationMs);

        await command.ExecuteNonQueryAsync();
    }

    private static Dictionary<string, string> ParseQuery(string requestUri)
    {
        var queryString = requestUri[(requestUri.IndexOf('?') + 1)..];

        return queryString
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));
    }

    private static long ToUnixTimeNanoseconds(DateTime timestamp) =>
        ToUnixTimeNanoseconds(new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)));

    private static long ToUnixTimeNanoseconds(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds() * 1_000_000;
}
