using System.Reflection;

namespace Stefan.Server.API.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => 
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            
            var parts = informationalVersion.Split('+', 2);
            var version = parts[0];
            var commitHash = parts.Length > 1 ? parts[1] : "unknown";
            
            return Results.Ok(new 
            { 
                Status = "Healthy",
                Version = version,
                CommitHash = commitHash
            });
        });
    }
}
