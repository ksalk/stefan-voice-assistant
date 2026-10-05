namespace Stefan.Server.API.Endpoints.Node;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("")
            .RequireAuthorization(AuthPolicy.NodePolicy);

        group.MapRegisterNodeEndpoint();
        group.MapProcessCommandEndpoint();
    }
}
