using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.ShoppingList;

public class ListShoppingListItemsTool(ToolsDbContext toolsDbContext) : ITool
{
    public string Name => nameof(ListShoppingListItemsTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(ListShoppingListItemsTool),
        functionDescription: "List all items in the shopping list",
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
        var allItems = await toolsDbContext.ShoppingListItems.ToListAsync(cancellationToken);

        if (allItems.Count == 0)
        {
            return "The shopping list is empty.";
        }

        var itemNames = allItems.Select(item => item.Name).ToList();
        return $"Shopping list: {string.Join(", ", itemNames)}";
    }
}
