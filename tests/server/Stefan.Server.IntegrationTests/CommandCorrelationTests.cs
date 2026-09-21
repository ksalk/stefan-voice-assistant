using System.Net;

namespace Stefan.Server.IntegrationTests;

public class CommandCorrelationTests : IntegrationTestBase
{
    private const string TestSecret = "test-secret";

    [Fact]
    public async Task PostCommand_ReturnsBadRequest_WhenCommandIdHeaderIsMissing()
    {
        await using var app = await CreateServerApp();

        var response = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "test-node",
            sessionId: "test-session",
            commandId: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommand_ReturnsBadRequest_WhenCommandIdHeaderIsNotAGuid()
    {
        await using var app = await CreateServerApp();

        var response = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "test-node",
            sessionId: "test-session",
            commandId: "not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommand_CorrelatesServerLogsWithCommandId()
    {
        var commandId = Guid.NewGuid().ToString("D");

        await using var app = await CreateServerApp();

        // The node is not registered, so the command is rejected with 401 after
        // the command id has been pushed into the log context.
        var response = await app.Client.PostCommandAsync(
            nodeSecret: TestSecret,
            deviceId: "unknown-node",
            sessionId: "test-session",
            commandId: commandId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var (stdout, _) = await app.GetLogsAsync();
        Assert.Contains(commandId, stdout);
    }
}
