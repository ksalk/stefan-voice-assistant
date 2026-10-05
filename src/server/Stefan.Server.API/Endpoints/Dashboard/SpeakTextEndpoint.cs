using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints.Dashboard;

public sealed class SpeakTextBody
{
    public required string Text { get; set; }
}

public static class SpeakTextEndpoint
{
    public static void MapSpeakTextEndpoint(this WebApplication app)
    {
        app.MapPost("/api/nodes/{nodeId:guid}/speak-text", async (
            Guid nodeId,
            [FromBody] SpeakTextBody body,
            [FromServices] SendNodeAudioMessage sendNodeAudioMessage,
            CancellationToken cancellationToken) =>
        {
            var result = await sendNodeAudioMessage.Handle(new SendNodeAudioMessageRequest
            {
                NodeId = nodeId,
                Text = body.Text,
            }, cancellationToken);

            return result.ToHttpResult(v => Results.Ok(new { message = "Audio sent", ttsDurationMs = v.TtsDurationMs }));
        })
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);
    }
}
