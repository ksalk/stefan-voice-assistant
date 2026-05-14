using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.HttpServer;
using Stefan.Node.Options;
using Stefan.Node.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);

builder.Services.Configure<NodeOptions>(builder.Configuration.GetSection(NodeOptions.SectionName));
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
builder.Services.Configure<RemoteServerOptions>(builder.Configuration.GetSection(RemoteServerOptions.SectionName));

// Voice command handling
builder.Services.AddSingleton<AppStateService>();
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

var app = builder.Build();

var remoteClient = app.Services.GetRequiredService<RemoteServerClient>();
if (!await remoteClient.RegisterNodeAsync())
{
    Console.Error.WriteLine("[fatal] Node registration failed. Exiting.");
    return 1;
}

var audioPlayer = app.Services.GetRequiredService<AudioPlayer>();
var sendFilePath = app.Configuration["send-file"];
if (sendFilePath is not null)
{
    Console.WriteLine($"[info] Sending file: {sendFilePath}");
    var audioBytes = await File.ReadAllBytesAsync(sendFilePath);
    var responseAudio = await remoteClient.SendCommandAsync(audioBytes);
    if (responseAudio is not null)
    {
        Console.WriteLine("[info] File sent successfully. Playing response...");
        await audioPlayer.PlayAsync(responseAudio);
        return 0;
    }
    Console.Error.WriteLine("[error] Failed to send file.");
    return 1;
}

await app.RunServerAsync("http://0.0.0.0:8080");
return 0;
