using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain;

namespace Stefan.Server.Infrastructure.EntityConfigurations;

public class CommandRecordEntityConfiguration : IEntityTypeConfiguration<CommandRecord>
{
    public void Configure(EntityTypeBuilder<CommandRecord> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NodeId).IsRequired();
        builder.Property(c => c.SessionId).IsRequired();
        builder.Property(c => c.ReceivedAt).IsRequired();

        builder.Property(c => c.InputAudio).IsRequired();
        builder.Property(c => c.InputAudioFormat).IsRequired();
        builder.Property(c => c.InputAudioDurationMs).IsRequired();

        builder.Property(c => c.Transcript).IsRequired(false);
        builder.Property(c => c.LlmConversationJson).IsRequired(false);
        builder.Property(c => c.ResponseText).IsRequired(false);

        builder.Property(c => c.OutputAudio).IsRequired(false);
        builder.Property(c => c.OutputAudioFormat).IsRequired(false);

        builder.Property(c => c.SttDurationMs).IsRequired(false);
        builder.Property(c => c.LlmDurationMs).IsRequired(false);
        builder.Property(c => c.TtsDurationMs).IsRequired(false);
        builder.Property(c => c.TotalDurationMs).IsRequired(false);

        builder.Property(c => c.Status).IsRequired();
    }
}
