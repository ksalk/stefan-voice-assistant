using Microsoft.Extensions.DependencyInjection;

namespace Stefan.Server.Application.Commands;

public static class CommandFeature
{
    public static IServiceCollection AddCommandFeatures(this IServiceCollection services)
    {
        services.AddScoped<ProcessCommand>();
        services.AddScoped<GetCommands>();
        services.AddScoped<GetCommand>();
        services.AddScoped<GetCommandAudio>();
        return services;
    }
}
