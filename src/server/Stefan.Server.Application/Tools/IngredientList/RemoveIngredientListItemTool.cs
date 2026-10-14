using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.IngredientList;

public class RemoveIngredientListItemTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "remove_ingredient_list_item";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "Remove an ingredient from the ingredient list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "The name of the ingredient to remove from the ingredient list."
                }
            },
            "required": [ "name" ]
        }
        """u8.ToArray())
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasName = argumentsJson.RootElement.TryGetProperty("name", out JsonElement name);

        string nameValue = hasName && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? throw new ArgumentNullException(nameof(name), "The name argument is required.")
            : throw new ArgumentException("The name argument is required and must be a string.", nameof(name));
        if (string.IsNullOrWhiteSpace(nameValue))
            throw new ArgumentException("The name argument must not be empty.", nameof(name));
        nameValue = nameValue.Trim();

        var existing = await documentStore.GetAsync<IngredientListDocument>(IngredientListDocument.DocumentId, cancellationToken);
        if (existing is null)
            throw new ArgumentException($"The ingredient '{nameValue}' does not exist in the ingredient list.", nameof(name));

        int removedCount = existing.Items.RemoveAll(
            i => string.Equals(i.Name, nameValue, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
            throw new ArgumentException($"The ingredient '{nameValue}' does not exist in the ingredient list.", nameof(name));

        // An emptied list stays as a persisted row with empty Items; the document
        // itself is only archived by clear_ingredient_list.
        await documentStore.UpdateAsync(existing, context.CommandId, cancellationToken);

        return removedCount == 1
            ? $"Removed '{nameValue}' from the ingredient list."
            : $"Removed {removedCount} entries of '{nameValue}' from the ingredient list.";
    }
}
