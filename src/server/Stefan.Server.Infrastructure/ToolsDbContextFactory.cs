using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stefan.Server.Infrastructure;

public class ToolsDbContextFactory : IDesignTimeDbContextFactory<ToolsDbContext>
{
    public ToolsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STEFAN_DB_CONNECTION_STRING")
            ?? "Host=localhost;Database=XXX;Username=XXX;Password=XXX";

        var optionsBuilder = new DbContextOptionsBuilder<ToolsDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.MigrationsHistoryTable("__ToolsMigrationsHistory"));

        return new ToolsDbContext(optionsBuilder.Options);
    }
}
