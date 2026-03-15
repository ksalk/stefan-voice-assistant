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
        builder.Property(n => n.Hostname).IsRequired();
        builder.Property(n => n.Port).IsRequired();
    }
}