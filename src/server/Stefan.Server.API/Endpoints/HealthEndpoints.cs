using System.Reflection;
using Microsoft.Extensions.Options;
using Stefan.Server.Application.AI;

namespace Stefan.Server.API.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (IConfiguration configuration, IOptions<OpenAiOptions> openAiOptions) =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            
            var parts = informationalVersion.Split('+', 2);
            var version = parts[0];
            var commitHash = parts.Length > 1 ? parts[1] : "unknown";

            var sttProvider = configuration["SttProvider"] ?? "Whisper";
            var ttsProvider = configuration["TtsProvider"] ?? "Piper";
            
            return Results.Ok(new 
            { 
                Status = "Healthy",
                Version = version,
                CommitHash = commitHash,
                SttProvider = sttProvider,
                TtsProvider = ttsProvider,
                LlmModel = openAiOptions.Value.Model
            });
        }).AllowAnonymous();
    }
}
