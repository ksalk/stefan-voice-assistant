using System.ClientModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Quartz;
using Stefan.Server.Application.AI;
using Stefan.Server.Application.AI.Tools.Timer;
using Stefan.Server.Application.Commands;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Nodes.Scheduling;
using Stefan.Server.Application.Queries;
using Stefan.Server.Application.Services;
using Whisper.net;

namespace Stefan.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNodeFeatures();
        services.AddCommandFeatures();

        services.AddSpeechToTextServices(configuration);

        services.AddTextToSpeechServices();

        services.AddAIServices(configuration);

        services.AddSingleton<AudioConverterService>();

        return services;
    }

    private static IServiceCollection AddNodeFeatures(this IServiceCollection services)
    {
        services.AddScoped<RegisterNode>();
        services.AddScoped<PingNode>();
        services.AddScoped<INodePingScheduler, NodePingScheduler>();
        return services;
    }

    private static IServiceCollection AddCommandFeatures(this IServiceCollection services)
    {
        services.AddScoped<ProcessCommand>();
        services.AddScoped<GetCommands>();
        return services;
    }

    private static IServiceCollection AddSpeechToTextServices(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["SttProvider"] ?? "Whisper";

        if (provider.Equals("Vosk", StringComparison.OrdinalIgnoreCase))
        {
            var voskModelPath = configuration["Vosk:ModelPath"] ?? "stt-models/vosk-model-en-us-0.22";
            services.AddSingleton<ISpeechToTextService>(new VoskSpeechToTextService(voskModelPath));
        }
        else
        {
            services.AddSingleton(_ =>
            {
                var factory = WhisperFactory.FromPath("ggml-base.bin");
                return factory.CreateBuilder()
                    .WithLanguage("en")
                    .Build();
            });

            services.AddSingleton<ISpeechToTextService, WhisperSpeechToTextService>();
        }

        return services;
    }

    private static IServiceCollection AddTextToSpeechServices(this IServiceCollection services)
    {
        services.AddSingleton<TextToSpeechService>();

        return services;
    }

    private static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiConfig>(configuration.GetSection(OpenAiConfig.SectionName));

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<OpenAiConfig>>().Value;

            return new ChatClient(
                model: config.Model,
                credential: new ApiKeyCredential(config.ApiKey),
                options: new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) });
        });

        services.AddDbContext<TimerDbContext>(options =>
        {
            options.UseSqlite(configuration.GetConnectionString("TimerDb"));
        });

        services.AddScoped<ITimerScheduler, TimerScheduler>();
        services.AddTransient<FireTimerJob>();
        services.AddScoped<LlmCommandService>();

        return services;
    }
}
