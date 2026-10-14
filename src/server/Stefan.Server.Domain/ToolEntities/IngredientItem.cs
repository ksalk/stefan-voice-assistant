namespace Stefan.Server.Domain.ToolEntities;

/// <summary>
/// One ingredient inside <see cref="IngredientListDocument"/>. Entries are matched by name
/// (case-insensitive) and the amount is free text exactly as spoken by the user.
/// </summary>
public class IngredientItem
{
    public string Name { get; set; } = null!;

    /// <summary>The amount as spoken by the user, e.g. "200g" or "2 pieces". Null when none was said.</summary>
    public string? Quantity { get; set; }
}
