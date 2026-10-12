using Microsoft.Extensions.DependencyInjection;
using Stefan.Server.Application.Tools.IngredientList;
using Stefan.Server.Application.Tools.ShoppingList;
using Stefan.Server.Application.Tools.Timer;

namespace Stefan.Server.Application.Tools;

public static class ToolFeature
{
    public static IServiceCollection AddToolFeatures(this IServiceCollection services)
    {
        services.AddScoped<ToolRegistry>();

        services.AddTimerFeatures();
        services.AddIngredientListFeatures();
        // TODO: to uncomment once Shopping List feature is available
        //services.AddShoppingListFeatures();

        return services;
    }
}
