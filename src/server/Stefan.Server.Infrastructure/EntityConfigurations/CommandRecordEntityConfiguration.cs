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

        builder.Property(c => c.Transcript).IsRequired();
        builder.Property(c => c.LlmConversationJson).IsRequired();
        builder.Property(c => c.ResponseText).IsRequired();

        builder.Property(c => c.OutputAudio).IsRequired();
        builder.Property(c => c.OutputAudioFormat).IsRequired();

        builder.Property(c => c.SttDurationMs).IsRequired();
        builder.Property(c => c.LlmDurationMs).IsRequired();
        builder.Property(c => c.TtsDurationMs).IsRequired();
        builder.Property(c => c.TotalDurationMs).IsRequired();

        builder.Property(c => c.Status).IsRequired();
    }
}
