using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.HttpServer;
using Stefan.Node.Options;
using Stefan.Node.Services;

var builder = WebApplication.CreateSlimBuilder(args);

ConfigureServices(builder);

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

await app.RunServerAsync("http://0.0.0.0:8080");
return 0;

WebApplicationBuilder ConfigureServices(WebApplicationBuilder builder)
{
    // Configuration
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddCommandLine(args);

    builder.Services.Configure<NodeOptions>(builder.Configuration.GetSection(NodeOptions.SectionName));
    builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
    builder.Services.Configure<RemoteServerOptions>(builder.Configuration.GetSection(RemoteServerOptions.SectionName));
    builder.Services.Configure<KeywordSpotterOptions>(builder.Configuration.GetSection(KeywordSpotterOptions.SectionName));

    // Voice command handling
    builder.Services.AddSingleton<AppStateService>();
    builder.Services.AddSingleton<IAudioInputProvider, MicAudioInputProvider>();
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
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    return builder;
}

async Task<bool> RegisterNode(WebApplication app)
{
    var remoteClient = app.Services.GetRequiredService<RemoteServerClient>();
    if (!await remoteClient.RegisterNodeAsync())
    {
        Console.Error.WriteLine("[fatal] Node registration failed. Exiting.");
        return false;
    }
    return true;
}

bool IsSendTestCommandRequested(WebApplication app, out string? sendFilePath)
{
    sendFilePath = app.Configuration["send-file"];
    return !string.IsNullOrWhiteSpace(sendFilePath);
}

async Task<bool> TrySendTestCommand(WebApplication app, string filePath)
{
    var remoteClient = app.Services.GetRequiredService<RemoteServerClient>();
    var audioPlayer = app.Services.GetRequiredService<AudioPlayer>();

    Console.WriteLine($"[info] Sending file: {filePath}");
    var audioBytes = await File.ReadAllBytesAsync(filePath!);
    var responseAudio = await remoteClient.SendCommandAsync(audioBytes);
    if (responseAudio is not null)
    {
        Console.WriteLine("[info] File sent successfully. Playing response...");
        await audioPlayer.PlayAsync(responseAudio);
        return true;
    }
    Console.Error.WriteLine("[error] Failed to send file.");
    return false;
}