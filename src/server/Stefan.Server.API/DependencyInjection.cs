using Microsoft.AspNetCore.Authentication;
using Stefan.Server.API.Options;
using Stefan.Server.Infrastructure.Authentication;

namespace Stefan.Server.API;

public static class DependencyInjection
{
    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<NodeSecretAuthenticationOptions, NodeSecretAuthenticationHandler>(
                NodeSecretAuthenticationOptions.DefaultScheme,
                _ => { })
            .AddScheme<DashboardAuthenticationOptions, DashboardAuthenticationHandler>(
                DashboardAuthenticationOptions.DefaultScheme,
                _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicy.NodePolicy, policy =>
            {
                policy.AddAuthenticationSchemes(NodeSecretAuthenticationOptions.DefaultScheme);
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy(AuthPolicy.DashboardPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(DashboardAuthenticationOptions.DefaultScheme);
                policy.RequireAuthenticatedUser();
            });

        return services;
    }

    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOptions = configuration.GetSection(DashboardCorsOptions.SectionName).Get<DashboardCorsOptions>()
                          ?? new DashboardCorsOptions();

        var origins = corsOptions.AllowedOrigins
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:Dashboard:AllowedOrigins is not configured. " +
                "Add 'Cors:Dashboard:AllowedOrigins' to appsettings with at least one origin (use '|' to separate multiple).");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy.DashboardPolicy, policy =>
            {
                policy.WithOrigins(origins);

                if (corsOptions.AllowAnyHeader)
                    policy.AllowAnyHeader();
                if (corsOptions.AllowAnyMethod)
                    policy.AllowAnyMethod();
            });
        });

        return services;
    }
}