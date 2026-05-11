using Microsoft.Extensions.Options;
using Stefan.Node.HttpServer;
using Stefan.Node.Options;
using Stefan.Node.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddHostedService<VoiceCommandDispatcher>();
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
builder.Services.Configure<RemoteServerOptions>(builder.Configuration.GetSection(RemoteServerOptions.SectionName));

builder.Services.AddHttpClient<RemoteServerClient>((sp, client) =>
{
    var remoteOptions = sp.GetRequiredService<IOptions<RemoteServerOptions>>().Value;
    client.BaseAddress = new Uri(remoteOptions.Url.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("X-Node-Secret", remoteOptions.AuthSecret);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

var registrationService = app.Services.GetRequiredService<RemoteServerClient>();
if (!await registrationService.RegisterNodeAsync())
{
    Console.Error.WriteLine("[fatal] Node registration failed. Exiting.");
    return 1;
}

await app.RunServerAsync("http://0.0.0.0:8080");
return 0;
