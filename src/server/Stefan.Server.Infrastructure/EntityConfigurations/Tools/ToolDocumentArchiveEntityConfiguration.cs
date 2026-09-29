using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Infrastructure.EntityConfigurations;

public class ToolDocumentArchiveEntityConfiguration : IEntityTypeConfiguration<ToolDocumentArchive>
{
    public void Configure(EntityTypeBuilder<ToolDocumentArchive> builder)
    {
        builder.ToTable("ToolDocumentArchive", "tools");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Type).IsRequired();
        builder.Property(d => d.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.ArchivedAt).IsRequired();

        builder.HasIndex(d => d.Type);
    }
}
