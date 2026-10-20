using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;

namespace Stefan.Server.Infrastructure.EntityConfigurations;

public class ToolDocumentArchiveEntityConfiguration : IEntityTypeConfiguration<ToolDocumentArchive>
{
    public void Configure(EntityTypeBuilder<ToolDocumentArchive> builder)
    {
        builder.ToTable("ToolDocumentArchive", "tools");

        // One archive row per delete event, so the key is not the document id.
        builder.HasKey(d => d.ArchiveId);

        // The database fills the key, so existing rows get one when the column is added.
        builder.Property(d => d.ArchiveId)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.Property(d => d.Type).IsRequired();
        builder.Property(d => d.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(d => d.CommandActions).IsRequired().HasColumnType("jsonb");
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.ArchivedAt).IsRequired();

        builder.HasIndex(d => d.Id);
        builder.HasIndex(d => d.Type);

        // Enables CommandActions @> '[{"commandId":"..."}]'::jsonb lookups.
        builder.HasIndex(d => d.CommandActions).HasMethod("gin").HasOperators("jsonb_path_ops");
    }
}
