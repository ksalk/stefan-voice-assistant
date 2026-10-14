namespace Stefan.Server.Domain.ToolEntities;

/// <summary>
/// Singleton document holding all ingredients of the ingredient list. The list lives
/// in one row with a fixed Id; Items carries the individual ingredients. An empty list
/// stays as a persisted row with empty Items: only clear_ingredient_list removes the
/// whole document (archiving it), remove and update always keep the row alive.
/// </summary>
public class IngredientListDocument : IToolDocument
{
    /// <summary>
    /// Fixed identity of the singleton row. Chosen once so all tools look up the same
    /// document without scanning by type.
    /// </summary>
    public static readonly Guid DocumentId = new("08f4d2e1-a3b5-4c6d-9e8f-101112131415");

    public static string DocumentType => "ingredient-list";

    public Guid Id { get; set; } = DocumentId;

    public List<IngredientItem> Items { get; set; } = [];
}
