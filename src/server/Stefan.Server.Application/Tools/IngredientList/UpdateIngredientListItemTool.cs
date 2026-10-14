using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.IngredientList;

public class UpdateIngredientListItemTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "update_ingredient_list_item";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "Update the quantity of an ingredient that is already on the ingredient list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "The name of the ingredient to update in the ingredient list."
                },
                "quantity": {
                    "type": "string",
                    "description": "The new amount of the ingredient exactly as spoken by the user, for example '300g' or '3 pieces'."
                }
            },
            "required": [ "name", "quantity" ]
        }
        """u8.ToArray())
    );

    public async Task<string> Execute(ChatToolCall toolCall, ToolCallContext context, CancellationToken cancellationToken = default)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
        bool hasName = argumentsJson.RootElement.TryGetProperty("name", out JsonElement name);
        bool hasQuantity = argumentsJson.RootElement.TryGetProperty("quantity", out JsonElement quantity);

        string nameValue = hasName && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? throw new ArgumentNullException(nameof(name), "The name argument is required.")
            : throw new ArgumentException("The name argument is required and must be a string.", nameof(name));
        if (string.IsNullOrWhiteSpace(nameValue))
            throw new ArgumentException("The name argument must not be empty.", nameof(name));
        nameValue = nameValue.Trim();

        string quantityValue = hasQuantity
            ? quantity.ValueKind switch
            {
                JsonValueKind.String => quantity.GetString(),
                JsonValueKind.Number => quantity.GetRawText(),
                JsonValueKind.Null => throw new ArgumentNullException(nameof(quantity), "The quantity argument is required."),
                _ => throw new ArgumentException("The quantity argument must be a string as spoken by the user, for example '300g'.", nameof(quantity))
            } ?? throw new ArgumentNullException(nameof(quantity), "The quantity argument is required.")
            : throw new ArgumentNullException(nameof(quantity), "The quantity argument is required.");
        if (string.IsNullOrWhiteSpace(quantityValue))
            throw new ArgumentException("The quantity argument must not be empty.", nameof(quantity));
        quantityValue = quantityValue.Trim();

        var existing = await documentStore.GetAsync<IngredientListDocument>(IngredientListDocument.DocumentId, cancellationToken);
        if (existing is null)
            throw new ArgumentException($"The ingredient '{nameValue}' does not exist in the ingredient list.", nameof(name));

        var matchingItems = existing.Items
            .Where(i => string.Equals(i.Name, nameValue, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingItems.Count == 0)
            throw new ArgumentException($"The ingredient '{nameValue}' does not exist in the ingredient list.", nameof(name));

        foreach (var item in matchingItems)
            item.Quantity = quantityValue;

        await documentStore.UpdateAsync(existing, context.CommandId, cancellationToken);

        return matchingItems.Count == 1
            ? $"Updated '{nameValue}' to quantity {quantityValue} in the ingredient list."
            : $"Updated {matchingItems.Count} entries of '{nameValue}' to quantity {quantityValue} in the ingredient list.";
    }
}
