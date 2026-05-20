using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.ShoppingList;

public class RemoveItemFromShoppingListTool(ToolsDbContext toolsDbContext) : ITool
{
    public string Name => nameof(RemoveItemFromShoppingListTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(RemoveItemFromShoppingListTool),
        functionDescription: "Remove an item from the shopping list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "item": {
                    "type": "string",
                    "description": "The item to remove from the shopping list."
                }
            },
            "required": [ "item" ]
        }
        """u8.ToArray())
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasItem = argumentsJson.RootElement.TryGetProperty("item", out JsonElement item);

        if (!hasItem)
            throw new ArgumentNullException(nameof(item), "The item argument is required.");

        string itemValue = item.GetString() ?? throw new ArgumentNullException(nameof(item), "The item argument is required.");

        var entry = await toolsDbContext.ShoppingListItems.FirstOrDefaultAsync(i => i.Name == itemValue, cancellationToken);
        if (entry == null)
            throw new ArgumentException($"The item '{itemValue}' does not exist in the shopping list.", nameof(item));

        toolsDbContext.ShoppingListItems.Remove(entry);
        await toolsDbContext.SaveChangesAsync(cancellationToken);

        return $"Removed '{itemValue}' from the shopping list.";
    }
}
