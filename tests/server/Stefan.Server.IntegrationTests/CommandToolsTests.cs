using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Stefan.Server.IntegrationTests;

public class CommandToolsTests : IntegrationTestBase
{
    private const string TestSecret = "test-secret";

    [Fact]
    public async Task GetCommandTools_ReturnsLiveAndArchivedDocumentsTouchedByCommand()
    {
        await using var app = await CreateServerApp(
            configureContainer: b => b.WithEnvironment("xAI__Endpoint", "http://127.0.0.1:1"));

        var commandId = await CreateCommandRecordAsync(app);

        var liveDocId = Guid.NewGuid();
        var archivedDocId = Guid.NewGuid();
        var otherCommandDocId = Guid.NewGuid();
        var otherCommandId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(app.DbConnectionString))
        {
            await connection.OpenAsync();

            await ExecAsync(connection, $$"""
                INSERT INTO tools."ToolDocuments" ("Id", "Type", "Payload", "CommandActions", "CreatedAt")
                VALUES ('{{liveDocId}}', 'shopping-item',
                        '{"id":"{{liveDocId}}","name":"milk"}',
                        '[{"CommandId":"{{commandId}}","Action":"created","AtUtc":"2026-01-01T10:00:00Z"}]',
                        '2026-01-01T10:00:00Z');
                """);

            await ExecAsync(connection, $$"""
                INSERT INTO tools."ToolDocuments" ("Id", "Type", "Payload", "CommandActions", "CreatedAt")
                VALUES ('{{otherCommandDocId}}', 'timer',
                        '{"id":"{{otherCommandDocId}}","durationInSeconds":60,"label":"tea","createdAt":"2026-01-01T11:00:00Z"}',
                        '[{"CommandId":"{{otherCommandId}}","Action":"created","AtUtc":"2026-01-01T11:00:00Z"}]',
                        '2026-01-01T11:00:00Z');
                """);

            await ExecAsync(connection, $$"""
                INSERT INTO tools."ToolDocumentArchive" ("Id", "Type", "Payload", "CommandActions", "CreatedAt", "ArchivedAt")
                VALUES ('{{archivedDocId}}', 'shopping-item',
                        '{"id":"{{archivedDocId}}","name":"bread"}',
                        '[{"CommandId":"{{commandId}}","Action":"created","AtUtc":"2026-01-01T12:00:00Z"},{"CommandId":"{{commandId}}","Action":"deleted","AtUtc":"2026-01-01T12:05:00Z"}]',
                        '2026-01-01T12:00:00Z',
                        '2026-01-01T12:05:00Z');
                """);
        }

        var response = await app.Client.GetCommandToolsAsync(commandId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetCommandToolsResult>()
                     ?? throw new InvalidOperationException("GetCommandTools response body was null.");

        // The archived document was created and deleted by this command, so it
        // appears once per action; the unrelated timer document is filtered out.
        Assert.Equal(3, result.Tools.Count);

        var liveTool = Assert.Single(result.Tools, t => t.Id == liveDocId);
        Assert.Equal("shopping-item", liveTool.Type);
        Assert.Equal("created", liveTool.Action);
        Assert.Equal("milk", liveTool.Payload.GetProperty("name").GetString());

        var archivedTools = result.Tools.Where(t => t.Id == archivedDocId).ToList();
        Assert.Equal(2, archivedTools.Count);
        Assert.Contains(archivedTools, t => t.Action == "created");
        Assert.Contains(archivedTools, t => t.Action == "deleted");
        Assert.Equal("bread", archivedTools[0].Payload.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCommandTools_ReturnsNotFound_WhenCommandDoesNotExist()
    {
        await using var app = await CreateServerApp();

        var response = await app.Client.GetCommandToolsAsync(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateCommandRecordAsync(ServerApp app)
    {
        var registration = await app.Client.PostRegisterNodeAsync(
            TestSecret, new RegisterNodeRequestDto("test-node", "test-session", 8080));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        var commandId = Guid.NewGuid();

        // STT fails with the dummy endpoint, but the command record is
        // persisted before transcription starts.
        var postResponse = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "test-node",
            sessionId: "test-session",
            commandId: commandId.ToString("D"));

        Assert.Equal(HttpStatusCode.InternalServerError, postResponse.StatusCode);

        await using (var connection = new NpgsqlConnection(app.DbConnectionString))
        {
            await connection.OpenAsync();
            await using var check = new NpgsqlCommand(
                """SELECT COUNT(*) FROM "CommandRecords" WHERE "Id" = @id""", connection);
            check.Parameters.AddWithValue("id", commandId);
            var count = (long)(await check.ExecuteScalarAsync())!;
            Assert.Equal(1, count);
        }

        return commandId;
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
