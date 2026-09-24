using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Stefan.Server.Application.AI;
using Stefan.Server.Application.Commands;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Scheduling;
using Stefan.Server.Application.Services;
using Stefan.Server.Application.Tools;
using Whisper.net;

namespace Stefan.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCommandFeatures();
        services.AddNodeFeatures();
        services.AddToolFeatures();

        services.AddSpeechToTextServices(configuration);

        services.AddTextToSpeechServices(configuration);

        services.AddAIServices(configuration);

        services.AddSingleton<AudioConverterService>();

        services.AddScoped<Scheduler>();

        services.AddHttpClient<NodeHttpClient>();

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
            var modelPath = configuration["Whisper:ModelPath"] ?? "ggml-base.bin";
            var modelUrl = configuration["Whisper:ModelUrl"] ?? WhisperModelDownloader.DefaultModelUrl;

            services.AddSingleton<WhisperModelDownloader>();
            services.AddSingleton(sp =>
            {
                var downloader = sp.GetRequiredService<WhisperModelDownloader>();
                downloader.EnsureModel(modelPath, modelUrl);

                var factory = WhisperFactory.FromPath(modelPath);
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

        return services;
    }
}
