using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Services;
using Stefan.Server.Infrastructure.DependencyInjection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var configuration = builder.Configuration;

    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext();

        AddOtlpSink(context.Configuration, loggerConfiguration);
    });

    builder.Services.AddOpenApi();

    builder.Services.AddApplication(configuration);
    builder.Services.AddInfrastructure(configuration);
    builder.Services.AddAuth();
    builder.Services.AddCors(configuration);

    var app = builder.Build();

    // Pushes the node supplied command id into the log context so every log event
    // produced while handling the request (including request logging) carries it.
    app.UseCommandCorrelation();
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // Reschedule ping jobs for all online nodes after server restart
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var rescheduleNodePings = services.GetRequiredService<RescheduleNodePings>();
        await rescheduleNodePings.Handle(CancellationToken.None);
    }

    // Eagerly load the STT model so it's ready before the first request.
    app.Services.GetRequiredService<ISpeechToTextService>();

    // Eagerly load the TTS engine (downloads piper/model if missing) so it's ready before the first request.
    app.Services.GetRequiredService<ITextToSpeechService>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapHealthEndpoints();
    app.MapNodeEndpoints();
    app.MapCommandEndpoints();


    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;

/// <summary>
/// Adds an OTLP/HTTP sink (e.g. the Alloy collector) when <c>Log:Otlp:Endpoint</c> is configured.
/// The endpoint must be the base URL; the sink appends the standard <c>/v1/logs</c> path itself.
/// </summary>
static void AddOtlpSink(IConfiguration configuration, Serilog.LoggerConfiguration loggerConfiguration)
{
    var otlpEndpoint = configuration["Log:Otlp:Endpoint"];
    if (string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        return;
    }

    loggerConfiguration.WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.HttpProtobuf;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = configuration["Log:Otlp:ServiceName"] is { Length: > 0 } serviceName
                ? serviceName
                : "stefan-server",
            ["service.namespace"] = "stefan",
            ["service.version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
        };

        foreach (var header in configuration.GetSection("Log:Otlp:Headers").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(header.Value))
            {
                options.Headers.Add(header.Key, header.Value);
            }
        }
    });
}
