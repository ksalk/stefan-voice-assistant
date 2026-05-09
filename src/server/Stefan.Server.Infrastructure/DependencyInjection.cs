using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Simpl;

namespace Stefan.Server.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    private const string ConnectionStringName = "StefanDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register infrastructure services here (e.g., database contexts, repositories, etc.)
        services.AddDbContext<StefanDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        // Configure Quartz with PostgreSQL persistence
        services.AddQuartz(q =>
        {
            q.UsePersistentStore(store =>
            {
                store.UsePostgres(postgres =>
                {
                    postgres.UseDriverDelegate<Quartz.Impl.AdoJobStore.PostgreSQLDelegate>();
                    postgres.ConnectionString = configuration.GetConnectionString(ConnectionStringName)!;
                    postgres.TablePrefix = "jobs.qrtz_";
                });
                store.UseSystemTextJsonSerializer();
            });
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}