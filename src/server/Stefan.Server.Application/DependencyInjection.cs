using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Stefan.Server.Application.AI;
using Stefan.Server.Application.Tools.Timer;
using Stefan.Server.Application.Commands;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Scheduling;
using Stefan.Server.Application.Services;
using Stefan.Server.Application.Tools;
using Whisper.net;
using Stefan.Server.Application.Nodes.Jobs;
using Stefan.Server.Application.Tools.Timer.Jobs;
using Stefan.Server.Application.Tools.ShoppingList;

namespace Stefan.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNodeFeatures();
        services.AddCommandFeatures();

        services.AddSpeechToTextServices(configuration);

        services.AddTextToSpeechServices(configuration);

        services.AddAIServices(configuration);

        services.AddSingleton<AudioConverterService>();

        services.AddScoped<Scheduler>();

        services.AddHttpClient<NodeHttpClient>();

        return services;
    }

    private static IServiceCollection AddNodeFeatures(this IServiceCollection services)
    {
        services.AddScoped<RegisterNode>();
        services.AddScoped<GetNodes>();
        services.AddScoped<GetNodeDetails>();
        services.AddScoped<PingNode>();
        services.AddScoped<ScheduleNodePing>();
        services.AddScoped<RescheduleNodePings>();

        services.AddScoped<PingNodeJob>();
        return services;
    }

    private static IServiceCollection AddCommandFeatures(this IServiceCollection services)
    {
        services.AddScoped<ProcessCommand>();
        services.AddScoped<GetCommands>();
        services.AddScoped<GetCommand>();
        services.AddScoped<GetCommandAudio>();
        return services;
    }

    private static IServiceCollection AddSpeechToTextServices(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["SttProvider"] ?? "Whisper";

        if (provider.Equals("Vosk", StringComparison.OrdinalIgnoreCase))
        {
            var voskModelPath = configuration["Vosk:ModelPath"] ?? "../../stt-models/vosk-model-en-us-0.22";
            services.AddSingleton<ISpeechToTextService>(new VoskSpeechToTextService(voskModelPath));
        }
        else if (provider.Equals("XAi", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISpeechToTextService, XAiSpeechToTextService>();
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

    private static IServiceCollection AddTextToSpeechServices(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["TtsProvider"] ?? "Piper";

        if (provider.Equals("XAi", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ITextToSpeechService, XAiTextToSpeechService>();
        }
        else
        {
            services.AddSingleton<ITextToSpeechService, PiperTextToSpeechService>();
        }

        return services;
    }

    private static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;

            return new ChatClient(
                model: config.Model,
                credential: new ApiKeyCredential(config.ApiKey),
                options: new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) });
        });

        services.AddScoped<LlmCommandService>();

        services.RegisterAITools();

        return services;
    }

    private static IServiceCollection RegisterAITools(this IServiceCollection services)
    {
        services.AddScoped<ToolRegistry>();

        services.AddScoped<ITool, AddTimerTool>();
        services.AddScoped<ITool, ListTimersTool>();
        services.AddScoped<ITool, CancelTimerTool>();
        services.AddScoped<ScheduleTimerJob>();
        services.AddScoped<CancelTimerJob>();
        services.AddScoped<FireTimerJob>();

        services.AddScoped<ITool, AddItemToShoppingListTool>();
        services.AddScoped<ITool, ListShoppingListItemsTool>();
        services.AddScoped<ITool, RemoveItemFromShoppingListTool>();
        services.AddScoped<ITool, ClearShoppingListTool>();

        return services;
    }
}
