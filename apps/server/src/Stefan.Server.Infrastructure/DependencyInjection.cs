using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stefan.Server.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    private const string ConnectionStringName = "StefanDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register infrastructure services here (e.g., database contexts, repositories, etc.)
        services.AddDbContext<StefanDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        return services;
    }
}