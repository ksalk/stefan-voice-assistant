using System.Buffers.Binary;
using System.Net;
using System.Text;
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

    [Fact]
    public async Task PostCommand_StoresWakeWordAudio_AndServesItFromAudioEndpoint()
    {
        await using var app = await CreateServerApp(
            configureContainer: b => b.WithEnvironment("xAI__Endpoint", "http://127.0.0.1:1"));

        var registration = await app.Client.PostRegisterNodeAsync(
            TestSecret, new RegisterNodeRequestDto("test-node", "test-session", 8080));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        var commandId = Guid.NewGuid();
        var wakeWordAudio = CreateWav(TimeSpan.FromMilliseconds(500));

        var response = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "test-node",
            sessionId: "test-session",
            commandId: commandId.ToString("D"),
            wakeWordAudio: wakeWordAudio);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var stored = await GetWakeWordAudioAsync(app.DbConnectionString, commandId);
        Assert.NotNull(stored.Audio);
        Assert.Equal("opus", stored.Format);
        Assert.True(stored.DurationMs > 0);

        var audioResponse = await app.Client.GetCommandAudioAsync(commandId, "WakeWord");
        Assert.Equal(HttpStatusCode.OK, audioResponse.StatusCode);
        Assert.Equal("audio/wav", audioResponse.Content.Headers.ContentType?.MediaType);

        var wav = await audioResponse.Content.ReadAsByteArrayAsync();
        Assert.True(wav.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
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

    private static async Task<(byte[]? Audio, string? Format, double? DurationMs)> GetWakeWordAudioAsync(
        string connectionString,
        Guid commandId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """SELECT "WakeWordAudio", "WakeWordAudioFormat", "WakeWordAudioDurationMs" FROM "CommandRecords" WHERE "Id" = @id""",
            connection);
        command.Parameters.AddWithValue("id", commandId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (null, null, null);
        }

        return (
            reader.IsDBNull(0) ? null : (byte[])reader[0],
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDouble(2));
    }

    private static byte[] CreateWav(TimeSpan duration)
    {
        const int sampleRate = 16_000;
        const int channels = 1;
        const int bitsPerSample = 16;
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = (int)(sampleRate * channels * bytesPerSample * duration.TotalSeconds);

        var wav = new byte[44 + dataSize];
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4), 36 + dataSize);
        "WAVE"u8.CopyTo(wav.AsSpan(8));
        "fmt "u8.CopyTo(wav.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), sampleRate * channels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32), (short)(channels * bytesPerSample));
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34), (short)bitsPerSample);
        "data"u8.CopyTo(wav.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40), dataSize);
        return wav;
    }
}
