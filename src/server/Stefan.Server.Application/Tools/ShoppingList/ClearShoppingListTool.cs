using OpenAI.Chat;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.ShoppingList;

public class ClearShoppingListTool(ToolsDbContext toolsDbContext) : ITool
{
    public string Name => nameof(ClearShoppingListTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(ClearShoppingListTool),
        functionDescription: "Clear all items from the shopping list",
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
        var allItems = toolsDbContext.ShoppingListItems;
        toolsDbContext.ShoppingListItems.RemoveRange(allItems);
        await toolsDbContext.SaveChangesAsync(cancellationToken);

        return "Cleared all items from the shopping list.";
    }
}
