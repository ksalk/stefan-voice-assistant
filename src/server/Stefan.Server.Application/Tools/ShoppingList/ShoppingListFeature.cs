using Microsoft.Extensions.DependencyInjection;

namespace Stefan.Server.Application.Tools.ShoppingList;

public static class ShoppingListFeature
{
    public static IServiceCollection AddShoppingListFeatures(this IServiceCollection services)
    {
        services.AddScoped<ITool, AddItemToShoppingListTool>();
        services.AddScoped<ITool, ListShoppingListItemsTool>();
        services.AddScoped<ITool, RemoveItemFromShoppingListTool>();
        services.AddScoped<ITool, ClearShoppingListTool>();
        return services;
    }
}
