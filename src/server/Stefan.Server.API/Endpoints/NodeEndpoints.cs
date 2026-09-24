using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Nodes;

namespace Stefan.Server.API.Endpoints;

public sealed class RegisterNodeBody
{
    public required string NodeName { get; set; }
    public required string SessionId { get; set; }
    public required int Port { get; set; }
}

public sealed class SpeakTextBody
{
    public required string Text { get; set; }
}

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/nodes/register", async (
            HttpContext context,
            [FromBody] RegisterNodeBody body,
            [FromServices] RegisterNode registerNode,
            CancellationToken cancellationToken) =>
        {
            var nodeIpAddress = GetNodeIpAddress(context);
            if(nodeIpAddress == null)
            {
                return Results.BadRequest("Unable to determine node IP address");
            }

            await registerNode.Handle(new RegisterNodeRequest
            {
                NodeName = body.NodeName,
                SessionId = body.SessionId,
                Port = body.Port,
                IpAddress = nodeIpAddress,
            }, cancellationToken);

            return Results.Ok();
        })
        .RequireAuthorization(AuthPolicy.NodePolicy);

        app.MapPost("/api/nodes/{nodeId:guid}/ping", async (
            Guid nodeId,
            [FromServices] PingNode pingNode,
            CancellationToken cancellationToken) =>
        {
            var result = await pingNode.Handle(new PingNodeRequest { NodeId = nodeId }, cancellationToken);

            return result.ToHttpResult(statusReport => statusReport == null
                ? Results.Ok()
                : Results.Ok(new
                {
                    statusReport.CpuUsage,
                    statusReport.MemoryUsage,
                    statusReport.DiskUsage,
                    statusReport.AudioVolume,
                    statusReport.Status
                }));
        })
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

        app.MapGet("/api/nodes/{nodeId:guid}", async (
            Guid nodeId,
            [FromServices] GetNodeDetails getNodeDetails,
            CancellationToken cancellationToken) =>
        {
            var result = await getNodeDetails.Handle(new GetNodeDetailsRequest { NodeId = nodeId }, cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

        app.MapGet("/api/nodes", async (
            [FromServices] GetNodes getNodes,
            CancellationToken cancellationToken) =>
        {
            var result = await getNodes.Handle(new GetNodesRequest(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthPolicy.DashboardPolicy)
        .RequireCors(CorsPolicy.DashboardPolicy);

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

    private static string? GetNodeIpAddress(HttpContext context)
    {
        var ip = context?.Connection?.RemoteIpAddress;
        if (ip != null && IPAddress.IsLoopback(ip))
        {
            ip = IPAddress.Loopback;
        }

        return ip?.ToString();
    }
}
