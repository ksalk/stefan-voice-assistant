namespace Stefan.Server.Domain.ToolEntities;

[ToolDocumentType("shopping-item")]
public class ShoppingListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
