using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Stefan.Server.Application.Logs;

public static class LogsFeature
{
    public static IServiceCollection AddLogFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LokiOptions>(configuration.GetSection(LokiOptions.SectionName));

        // Registered unconditionally so the endpoint exists even when Loki is not configured;
        // GetCommandLogs answers with a clean error instead in that case.
        services.AddHttpClient(LokiHttpClient.ClientName, (sp, client) =>
        {
            var lokiOptions = sp.GetRequiredService<IOptions<LokiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(lokiOptions.TimeoutSeconds);
            foreach (var header in lokiOptions.Headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            if (!string.IsNullOrWhiteSpace(lokiOptions.Url))
            {
                client.BaseAddress = new Uri(lokiOptions.Url.TrimEnd('/') + "/");
            }
        });

        services.AddScoped<LokiHttpClient>();
        services.AddScoped<GetCommandLogs>();

        return services;
    }
}
