using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Stefan.Server.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
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

        services.AddScoped<LlmCommandService>();

        return services;
    }
}
