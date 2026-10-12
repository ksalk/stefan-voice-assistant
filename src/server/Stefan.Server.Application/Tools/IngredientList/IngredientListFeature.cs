using Microsoft.Extensions.DependencyInjection;

namespace Stefan.Server.Application.Tools.IngredientList;

public static class IngredientListFeature
{
    public static IServiceCollection AddIngredientListFeatures(this IServiceCollection services)
    {
        services.AddScoped<ITool, AddIngredientListItemTool>();
        services.AddScoped<ITool, ListIngredientListItemsTool>();
        services.AddScoped<ITool, UpdateIngredientListItemTool>();
        services.AddScoped<ITool, RemoveIngredientListItemTool>();
        services.AddScoped<ITool, ClearIngredientListTool>();
        return services;
    }
}
