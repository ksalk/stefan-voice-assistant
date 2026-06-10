using Microsoft.EntityFrameworkCore;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure.EntityConfigurations;

namespace Stefan.Server.Infrastructure;

public class StefanDbContext : DbContext
{
    public StefanDbContext(DbContextOptions<StefanDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new NodeEntityConfiguration());
        modelBuilder.ApplyConfiguration(new NodeStatusReportEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CommandRecordEntityConfiguration());
    }

    public DbSet<Node> Nodes { get; set; }
    public DbSet<NodeStatusReport> NodeStatusReports { get; set; }
    public DbSet<CommandRecord> CommandRecords { get; set; }
}