namespace Stefan.Server.Domain.ToolEntities;

public class ShoppingListItem : IToolDocument
{
    public static string DocumentType => "shopping-item";

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
