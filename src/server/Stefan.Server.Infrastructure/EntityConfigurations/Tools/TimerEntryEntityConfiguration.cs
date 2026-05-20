namespace Stefan.Server.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;
using Stefan.Server.Domain.ToolEntities;

public class TimerEntryEntityConfiguration : IEntityTypeConfiguration<TimerEntry>
{
    public void Configure(EntityTypeBuilder<TimerEntry> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.DurationInSeconds).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.Label).IsRequired(false);
    }
}