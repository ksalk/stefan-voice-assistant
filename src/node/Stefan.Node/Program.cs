using Microsoft.Extensions.Options;
using Serilog;
using Stefan.Node.Audio;
using Stefan.Node.HttpServer;
using Stefan.Node.Logging;
using Stefan.Node.Options;
using Stefan.Node.Services;

var builder = WebApplication.CreateSlimBuilder(args);

Log.Logger = NodeLogger.Create(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

ConfigureServices(builder);

try
{
    var app = builder.Build();

    if (!await RegisterNode(app))
    {
        return 1;
    }

    if (IsSendTestCommandRequested(app, out var sendFilePath))
    {
        await TrySendTestCommand(app, sendFilePath!);
        return 0;
    }

    if (IsPlayFileCommandRequested(app, out var playFilePath))
    {
        var audioPlayer = app.Services.GetRequiredService<AudioPlayer>();
        await audioPlayer.PlayAsync(await File.ReadAllBytesAsync(playFilePath!));
        return 0;
    }

    var serverUrl = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Url;
    await app.RunServerAsync(serverUrl);
    return 0;
}
finally
{
    Log.CloseAndFlush();
}

WebApplicationBuilder ConfigureServices(WebApplicationBuilder builder)
{
    // Clear default configuration sources to control order
    builder.Configuration.Sources.Clear();
    
    // Configuration - JSON files first, then env vars, then command line
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    builder.Services.Configure<NodeOptions>(builder.Configuration.GetSection(NodeOptions.SectionName));
    builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
    builder.Services.Configure<RemoteServerOptions>(builder.Configuration.GetSection(RemoteServerOptions.SectionName));
    builder.Services.Configure<KeywordSpotterOptions>(builder.Configuration.GetSection(KeywordSpotterOptions.SectionName));
    builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection(AudioOptions.SectionName));

    // Voice command handling
    builder.Services.AddSingleton<AppStateService>();
    RegisterAudioInputProvider(builder);
    builder.Services.AddHostedService<VoiceCommandDispatcher>();

    // Audio player
    builder.Services.AddSingleton<AudioPlayer>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AudioPlayer>());

    // Remote server communication
    builder.Services.AddHttpClient<RemoteServerClient>((sp, client) =>
    {
        var remoteOptions = sp.GetRequiredService<IOptions<RemoteServerOptions>>().Value;
        client.BaseAddress = new Uri(remoteOptions.Url.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("X-Node-Secret", remoteOptions.AuthSecret);
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    return builder;
}

void RegisterAudioInputProvider(WebApplicationBuilder builder)
{
    var audioOptions = builder.Configuration.GetSection(AudioOptions.SectionName).Get<AudioOptions>() ?? new AudioOptions();
    var inputSource = audioOptions.InputSource.ToLowerInvariant();

    Log.Information("[audio] Input source: {InputSource}", inputSource);

    switch (inputSource)
    {
        case "pipe":
            builder.Services.AddSingleton<IAudioInputProvider, PipeAudioInputProvider>();
            Log.Information("[audio] Using pipe input (path: {PipePath})", audioOptions.PipePath ?? "/tmp/audio-input");
            break;
        case "mic":
        default:
            builder.Services.AddSingleton<IAudioInputProvider, MicAudioInputProvider>();
            Log.Information("[audio] Using microphone input");
            break;
    }
}

async Task<bool> RegisterNode(WebApplication app)
{
    var remoteClient = app.Services.GetRequiredService<RemoteServerClient>();
    var result = await remoteClient.RegisterNodeAsync();
    if (!result.IsSuccess)
    {
        Log.Error("[fatal] Node registration failed. {Error}. Exiting.", result.Error);
        return false;
    }
    return true;
}

bool IsSendTestCommandRequested(WebApplication app, out string? sendFilePath)
{
    sendFilePath = app.Configuration["send-file"];
    return !string.IsNullOrWhiteSpace(sendFilePath);
}

bool IsPlayFileCommandRequested(WebApplication app, out string? playFilePath)
{
    playFilePath = app.Configuration["play-file"];
    return !string.IsNullOrWhiteSpace(playFilePath);
}

async Task<bool> TrySendTestCommand(WebApplication app, string filePath)
{
    var remoteClient = app.Services.GetRequiredService<RemoteServerClient>();
    var audioPlayer = app.Services.GetRequiredService<AudioPlayer>();

    Log.Information("[info] Sending file: {FilePath}", filePath);
    var audioBytes = await File.ReadAllBytesAsync(filePath!);
    var result = await remoteClient.SendCommandAsync(audioBytes, Guid.NewGuid());
    if (result.IsSuccess)
    {
        Log.Information("[info] File sent successfully. Response: {ResponseText}", result.Value.ResponseText);
        await audioPlayer.PlayAsync(result.Value.Audio);
        return true;
    }
    Log.Error("[error] Failed to send file: {Error}", result.Error);
    return false;
}
