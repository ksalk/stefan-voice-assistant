using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Nodes.Scheduling;
using Stefan.Server.Application.Services;
using Whisper.net;

namespace Stefan.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNodeFeatures();

        services.AddSpeechToTextServices();

        services.AddTextToSpeechServices();

        return services;
    }

    private static IServiceCollection AddNodeFeatures(this IServiceCollection services)
    {
        services.AddScoped<RegisterNode>();
        services.AddScoped<INodePingScheduler, NodePingScheduler>();
        return services;
    }

    private static IServiceCollection AddSpeechToTextServices(this IServiceCollection services)
    {
        services.AddSingleton(_ =>
        {
            var factory = WhisperFactory.FromPath("ggml-base.bin");
            return factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
        });

        services.AddSingleton<SpeechToTextService>();

        return services;
    }

    private static IServiceCollection AddTextToSpeechServices(this IServiceCollection services)
    {
        services.AddSingleton<TextToSpeechService>();

        return services;
    }
}
