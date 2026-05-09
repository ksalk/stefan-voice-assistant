namespace Stefan.Node.HttpServer;

/// <summary>
/// A simple HTTP server for handling requests from central server.
/// </summary>
public static class HttpServer
{
    public static async Task RunServerAsync(this WebApplication app, string url)
    {
        app.MapEndpoints();

        await app.RunAsync(url);
    }

    private static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => "OK");
        
        return app;
    }
}