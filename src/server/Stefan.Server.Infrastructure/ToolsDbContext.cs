using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Infrastructure;

public class ToolsDbContext : DbContext
{
    public ToolsDbContext(DbContextOptions<ToolsDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("tools");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ToolsDbContext).Assembly);
    }

    public DbSet<TimerEntry> TimerEntries { get; set; }
}