
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stefan.Server.Domain.ToolEntities;

namespace Stefan.Server.Infrastructure.EntityConfigurations;
public class ShoppingListItemEntityConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Name).IsRequired();
    }
}