using System.Text.Json;
using OpenAI.Chat;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Tools.IngredientList;

public class AddIngredientListItemTool(IToolDocumentStore documentStore) : ITool
{
    public string Name => "add_ingredient_list_item";

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription: "Add an ingredient to the ingredient list",
        functionParameters: BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "The name of the ingredient to add to the ingredient list."
                },
                "quantity": {
                    "type": "string",
                    "description": "The amount of the ingredient exactly as spoken by the user, for example '200g' or '2 pieces'. Omit when the user did not say an amount."
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
        bool hasQuantity = argumentsJson.RootElement.TryGetProperty("quantity", out JsonElement quantity);

        if (!hasName)
            throw new ArgumentNullException(nameof(name), "The name argument is required.");

        string nameValue = name.GetString() ?? throw new ArgumentNullException(nameof(name), "The name argument is required.");
        if (string.IsNullOrWhiteSpace(nameValue))
            throw new ArgumentException("The name argument must not be empty.", nameof(name));
        nameValue = nameValue.Trim();

        string? quantityValue = null;
        if (hasQuantity && quantity.ValueKind is not JsonValueKind.Null)
        {
            quantityValue = (quantity.ValueKind switch
            {
                JsonValueKind.String => quantity.GetString(),
                JsonValueKind.Number => quantity.GetRawText(),
                _ => throw new ArgumentException("The quantity argument must be a string as spoken by the user, for example '200g'.", nameof(quantity))
            })?.Trim();
        }
        if (string.IsNullOrWhiteSpace(quantityValue))
            quantityValue = null;

        var entry = new IngredientListItem
        {
            Id = Guid.NewGuid(),
            Name = nameValue,
            Quantity = quantityValue
        };

        await documentStore.AddAsync(entry, context.CommandId, cancellationToken);

        return quantityValue is null
            ? $"Added '{nameValue}' to the ingredient list."
            : $"Added '{nameValue}' with quantity {quantityValue} to the ingredient list.";
    }
}
