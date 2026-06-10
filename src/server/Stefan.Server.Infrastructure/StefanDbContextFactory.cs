using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stefan.Server.Infrastructure;

public class StefanDbContextFactory : IDesignTimeDbContextFactory<StefanDbContext>
{
    public StefanDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STEFAN_DB_CONNECTION_STRING")
            ?? "Host=localhost;Database=XXX;Username=XXX;Password=XXX";

        var optionsBuilder = new DbContextOptionsBuilder<StefanDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new StefanDbContext(optionsBuilder.Options);
    }
}
