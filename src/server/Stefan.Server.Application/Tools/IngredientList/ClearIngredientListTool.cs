using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.IngredientList;

public class ClearIngredientListTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "clear_ingredient_list";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "Clear all items from the ingredient list",
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
        // The document is removed entirely, which archives it with a "deleted" provenance
        // entry. The next add re-creates the singleton row.
        var existing = await documentStore.GetAsync<IngredientListDocument>(IngredientListDocument.DocumentId, cancellationToken);
        if (existing is not null)
            await documentStore.DeleteAsync<IngredientListDocument>(existing.Id, context.CommandId, cancellationToken);

        return "Cleared all items from the ingredient list.";
    }
}
