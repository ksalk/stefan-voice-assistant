using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.ShoppingList;

public class AddItemToShoppingListTool(ToolsDbContext toolsDbContext) : ITool
{
    public string Name => nameof(AddItemToShoppingListTool);

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: nameof(AddItemToShoppingListTool),
        functionDescription: "Add an item to the shopping list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "item": {
                    "type": "string",
                    "description": "The item to add to the shopping list."
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

        var entry = new ShoppingListItem
        {
            Name = itemValue
        };

        toolsDbContext.ShoppingListItems.Add(entry);
        await toolsDbContext.SaveChangesAsync(cancellationToken);

        return $"Added '{itemValue}' to the shopping list.";
    }
}
