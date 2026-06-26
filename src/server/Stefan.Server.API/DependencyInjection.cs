using Microsoft.AspNetCore.Authentication;
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
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy.DashboardPolicy, policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}