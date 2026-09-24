using System.Net;
using Npgsql;

namespace Stefan.Server.IntegrationTests;

public class ProcessCommandTests : IntegrationTestBase
{
    private const string TestSecret = "test-secret";
    private const int SttFailed = 1;

    [Fact]
    public async Task PostCommand_ReturnsServerErrorAndRecordsSttFailure_WhenTranscriptionFails()
    {
        await using var app = await CreateServerApp(
            configureContainer: b => b.WithEnvironment("xAI__Endpoint", "http://127.0.0.1:1"));

        var registration = await app.Client.PostRegisterNodeAsync(
            TestSecret, new RegisterNodeRequestDto("test-node", "test-session", 8080));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        var commandId = Guid.NewGuid();

        var response = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "test-node",
            sessionId: "test-session",
            commandId: commandId.ToString("D"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Speech recognition failed", body);

        var status = await GetCommandStatusAsync(app.DbConnectionString, commandId);
        Assert.Equal(SttFailed, status);
    }

    private static async Task<int> GetCommandStatusAsync(string connectionString, Guid commandId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """SELECT "Status" FROM "CommandRecords" WHERE "Id" = @id""", connection);
        command.Parameters.AddWithValue("id", commandId);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
