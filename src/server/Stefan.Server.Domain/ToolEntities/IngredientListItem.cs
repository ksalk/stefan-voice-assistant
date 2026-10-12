namespace Stefan.Server.Domain.ToolEntities;

public class IngredientListItem : IToolDocument
{
    public static string DocumentType => "ingredient-item";

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>The amount as spoken by the user, e.g. "200g" or "2 pieces". Null when none was said.</summary>
    public string? Quantity { get; set; }
}
