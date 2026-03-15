namespace Stefan.Server.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;

public class NodeEntityConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Name).IsRequired();
        builder.Property(n => n.IpAddress).IsRequired();
        builder.Property(n => n.Port).IsRequired();
        builder.Property(n => n.CurrentSessionId).IsRequired();
        builder.Property(n => n.Status).IsRequired();
        builder.Property(n => n.RegisteredAt).IsRequired();
        builder.Property(n => n.RestartCount).IsRequired();
    }
}