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

        if (!hasName)
            throw new ArgumentNullException(nameof(name), "The name argument is required.");

        string nameValue = name.GetString() ?? throw new ArgumentNullException(nameof(name), "The name argument is required.");
        if (string.IsNullOrWhiteSpace(nameValue))
            throw new ArgumentException("The name argument must not be empty.", nameof(name));
        nameValue = nameValue.Trim();

        var matchingItems = await documentStore.ListAsync<IngredientListItem>(
            i => string.Equals(i.Name, nameValue, StringComparison.OrdinalIgnoreCase), cancellationToken);

        if (matchingItems.Count == 0)
            throw new ArgumentException($"The ingredient '{nameValue}' does not exist in the ingredient list.", nameof(name));

        foreach (var entry in matchingItems)
            await documentStore.DeleteAsync<IngredientListItem>(entry.Id, context.CommandId, cancellationToken);

        return matchingItems.Count == 1
            ? $"Removed '{nameValue}' from the ingredient list."
            : $"Removed {matchingItems.Count} entries of '{nameValue}' from the ingredient list.";
    }
}
