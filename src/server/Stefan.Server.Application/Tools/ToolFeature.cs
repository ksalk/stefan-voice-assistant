using Microsoft.Extensions.DependencyInjection;
using Stefan.Server.Application.Tools.ShoppingList;
using Stefan.Server.Application.Tools.Timer;

namespace Stefan.Server.Application.Tools;

public static class ToolFeature
{
    public static IServiceCollection AddToolFeatures(this IServiceCollection services)
    {
        services.AddScoped<ToolRegistry>();

        services.AddTimerFeatures();
        services.AddShoppingListFeatures();

        return services;
    }
}
