namespace Stefan.Server.API.Endpoints.Node;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        app.MapRegisterNodeEndpoint();
        app.MapProcessCommandEndpoint();
    }
}
