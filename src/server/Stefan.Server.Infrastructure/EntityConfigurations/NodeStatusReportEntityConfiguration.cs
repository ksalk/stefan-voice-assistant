namespace Stefan.Server.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;

public class NodeStatusReportEntityConfiguration : IEntityTypeConfiguration<NodeStatusReport>
{
    public void Configure(EntityTypeBuilder<NodeStatusReport> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NodeId).IsRequired();
        builder.Property(n => n.Timestamp).IsRequired();
        builder.Property(n => n.Status).IsRequired();
    }
}