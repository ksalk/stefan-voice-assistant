using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stefan.Server.Application;
using Stefan.Server.Infrastructure.DependencyInjection;
using ApiDeps = Stefan.Server.API.DependencyInjection;

namespace Stefan.Server.DI.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void CompositionRoot_ValidatesAllRegistrationsOnBuild()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:StefanDb"] = "Host=localhost;Database=stefan_test;Username=test;Password=test",
            ["SttProvider"] = "XAi",
            ["TtsProvider"] = "XAi",
            ["OpenAI:ApiKey"] = "test-key",
            ["OpenAI:Model"] = "test-model",
            ["OpenAI:Endpoint"] = "https://localhost",
            ["Cors:Dashboard:AllowedOrigins"] = "http://localhost",
        });

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        ApiDeps.AddAuth(builder.Services);
        ApiDeps.AddCors(builder.Services, builder.Configuration);

        using var app = builder.Build();
    }
}
