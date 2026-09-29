using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;

namespace Stefan.Server.Infrastructure.EntityConfigurations;

public class ToolDocumentEntityConfiguration : IEntityTypeConfiguration<ToolDocument>
{
    public void Configure(EntityTypeBuilder<ToolDocument> builder)
    {
        builder.ToTable("ToolDocuments", "tools");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Type).IsRequired();
        builder.Property(d => d.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasIndex(d => d.Type);
    }
}
