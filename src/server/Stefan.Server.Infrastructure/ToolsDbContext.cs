using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure.EntityConfigurations;

namespace Stefan.Server.Infrastructure;

public class ToolsDbContext : DbContext
{
    public ToolsDbContext(DbContextOptions<ToolsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("tools");
        modelBuilder.ApplyConfiguration(new ShoppingListItemEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TimerEntryEntityConfiguration());
    }

    public DbSet<TimerEntry> TimerEntries { get; set; }
    public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
}