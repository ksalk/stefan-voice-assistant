using Microsoft.Extensions.DependencyInjection;
using Stefan.Server.Application.Tools.Timer.Jobs;

namespace Stefan.Server.Application.Tools.Timer;

public static class TimerFeature
{
    public static IServiceCollection AddTimerFeatures(this IServiceCollection services)
    {
        services.AddScoped<ITool, AddTimerTool>();
        services.AddScoped<ITool, ListTimersTool>();
        services.AddScoped<ITool, CancelTimerTool>();
        services.AddScoped<ScheduleTimerJob>();
        services.AddScoped<CancelTimerJob>();
        services.AddScoped<FireTimerJob>();
        return services;
    }
}
