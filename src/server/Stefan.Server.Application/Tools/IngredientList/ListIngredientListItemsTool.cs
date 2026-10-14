using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.IngredientList;

public class ListIngredientListItemsTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "list_ingredient_list_items";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "List all ingredients in the ingredient list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """u8.ToArray())
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        var existing = await documentStore.GetAsync<IngredientListDocument>(IngredientListDocument.DocumentId, cancellationToken);

        if (existing is null || existing.Items.Count == 0)
        {
            return "The ingredient list is empty.";
        }

        var itemDescriptions = existing.Items
            .Select(item => string.IsNullOrEmpty(item.Quantity) ? item.Name : $"{item.Name} {item.Quantity}")
            .ToList();
        return $"Ingredient list: {string.Join(", ", itemDescriptions)}";
    }
}
