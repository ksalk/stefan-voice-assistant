namespace Stefan.Server.API.Endpoints.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGetNodesEndpoint();
        app.MapGetNodeDetailsEndpoint();
        app.MapPingNodeEndpoint();
        app.MapSpeakTextEndpoint();
        app.MapGetCommandsEndpoint();
        app.MapGetCommandEndpoint();
        app.MapGetCommandAudioEndpoint();
    }
}
