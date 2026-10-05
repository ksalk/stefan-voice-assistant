using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.ShoppingList;

public class ClearShoppingListTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "clear_shopping_list";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
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
        var allItems = await documentStore.ListAsync<ShoppingListItem>(cancellationToken: cancellationToken);
        foreach (var item in allItems)
            await documentStore.DeleteAsync<ShoppingListItem>(item.Id, context.CommandId, cancellationToken);

        return "Cleared all items from the shopping list.";
    }
}
