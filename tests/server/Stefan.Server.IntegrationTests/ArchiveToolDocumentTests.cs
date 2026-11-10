using System.Net;
using System.Text.Json;
using Npgsql;

namespace Stefan.Server.IntegrationTests;

public class ArchiveToolDocumentTests : IntegrationTestBase
{
    [Fact]
    public async Task ArchiveToolDocument_MovesLiveDocumentToArchive()
    {
        await using var app = await CreateServerApp();

        var documentId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await InsertLiveDocumentAsync(
            app,
            documentId,
            "shopping-item",
            $$"""{"id":"{{documentId}}","name":"milk"}""",
            $$"""[{"CommandId":"{{commandId}}","Action":"created","AtUtc":"2026-01-02T03:04:05Z"}]""",
            createdAt);

        var response = await app.Client.ArchiveToolDocumentAsync(documentId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountRowsAsync(app, "ToolDocuments", documentId));
        Assert.Equal(1, await CountRowsAsync(app, "ToolDocumentArchive", documentId));

        var archived = await ReadArchivedDocumentAsync(app, documentId);

        Assert.Equal("shopping-item", archived.Type);
        Assert.Equal(createdAt, archived.CreatedAt);
        Assert.True(archived.ArchivedAt > createdAt);
        Assert.NotEqual(Guid.Empty, archived.ArchiveId);

        using var payload = JsonDocument.Parse(archived.Payload);
        Assert.Equal("milk", payload.RootElement.GetProperty("name").GetString());

        using var actions = JsonDocument.Parse(archived.CommandActions);
        var action = Assert.Single(actions.RootElement.EnumerateArray());
        Assert.Equal("created", action.GetProperty("Action").GetString());
    }

    [Fact]
    public async Task ArchiveToolDocument_ArchivesTimer()
    {
        await using var app = await CreateServerApp();

        var documentId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-1);

        await InsertLiveDocumentAsync(
            app,
            documentId,
            "timer",
            $$"""{"id":"{{documentId}}","durationInSeconds":60,"label":"tea","createdAt":"2026-01-02T03:04:05Z"}""",
            "[]",
            createdAt);

        var response = await app.Client.ArchiveToolDocumentAsync(documentId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountRowsAsync(app, "ToolDocuments", documentId));
        Assert.Equal(1, await CountRowsAsync(app, "ToolDocumentArchive", documentId));
    }

    [Fact]
    public async Task ArchiveToolDocument_ReturnsNotFound_WhenNoLiveDocumentExists()
    {
        await using var app = await CreateServerApp();

        var unknownResponse = await app.Client.ArchiveToolDocumentAsync(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        var archivedOnlyId = Guid.NewGuid();
        await InsertArchivedDocumentAsync(app, archivedOnlyId);

        var archivedOnlyResponse = await app.Client.ArchiveToolDocumentAsync(archivedOnlyId);

        Assert.Equal(HttpStatusCode.NotFound, archivedOnlyResponse.StatusCode);
        Assert.Equal(0, await CountRowsAsync(app, "ToolDocuments", archivedOnlyId));
        Assert.Equal(1, await CountRowsAsync(app, "ToolDocumentArchive", archivedOnlyId));
    }

    private static async Task InsertLiveDocumentAsync(
        ServerApp app,
        Guid id,
        string type,
        string payload,
        string commandActions,
        DateTime createdAt)
    {
        await using var connection = new NpgsqlConnection(app.DbConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO tools."ToolDocuments" ("Id", "Type", "Payload", "CommandActions", "CreatedAt")
            VALUES (@id, @type, @payload::jsonb, @commandActions::jsonb, @createdAt)
            """,
            connection);

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("payload", payload);
        command.Parameters.AddWithValue("commandActions", commandActions);
        command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertArchivedDocumentAsync(ServerApp app, Guid id)
    {
        await using var connection = new NpgsqlConnection(app.DbConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO tools."ToolDocumentArchive" ("ArchiveId", "Id", "Type", "Payload", "CommandActions", "CreatedAt", "ArchivedAt")
            VALUES (@archiveId, @id, 'shopping-item', '{"name":"bread"}'::jsonb, '[]'::jsonb, @createdAt, @archivedAt)
            """,
            connection);

        command.Parameters.AddWithValue("archiveId", Guid.NewGuid());
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("createdAt", DateTime.UtcNow.AddHours(-2));
        command.Parameters.AddWithValue("archivedAt", DateTime.UtcNow.AddHours(-1));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountRowsAsync(ServerApp app, string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(app.DbConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"""SELECT COUNT(*) FROM tools."{table}" WHERE "Id" = @id""",
            connection);
        command.Parameters.AddWithValue("id", id);

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<ArchivedDocument> ReadArchivedDocumentAsync(ServerApp app, Guid id)
    {
        await using var connection = new NpgsqlConnection(app.DbConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT "Type", "Payload"::text, "CommandActions"::text, "CreatedAt", "ArchivedAt", "ArchiveId"
            FROM tools."ToolDocumentArchive"
            WHERE "Id" = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new ArchivedDocument(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDateTime(3),
            reader.GetDateTime(4),
            reader.GetGuid(5));
    }

    private sealed record ArchivedDocument(
        string Type,
        string Payload,
        string CommandActions,
        DateTime CreatedAt,
        DateTime ArchivedAt,
        Guid ArchiveId);
}
