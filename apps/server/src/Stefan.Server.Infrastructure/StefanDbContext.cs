using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;

namespace Stefan.Server.Infrastructure;

public class StefanDbContext : DbContext
{
    public StefanDbContext(DbContextOptions<StefanDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StefanDbContext).Assembly);
    }

    public DbSet<Node> Nodes { get; set; }
    public DbSet<NodeStatusReport> NodeStatusReports { get; set; }
}