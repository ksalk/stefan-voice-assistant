namespace Stefan.Server.API.Endpoints.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("")
            .RequireAuthorization(AuthPolicy.DashboardPolicy)
            .RequireCors(CorsPolicy.DashboardPolicy);

        group.MapGetNodesEndpoint();
        group.MapGetNodeDetailsEndpoint();
        group.MapPingNodeEndpoint();
        group.MapSpeakTextEndpoint();
        group.MapGetCommandsEndpoint();
        group.MapGetCommandEndpoint();
        group.MapGetCommandAudioEndpoint();
    }
}
