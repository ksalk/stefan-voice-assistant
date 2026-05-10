using Stefan.Node.HttpServer;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// builder.Services.Configure<KeywordSpotterOptions>(
//     builder.Configuration.GetSection("KeywordSpotter"));

builder.Services.AddHostedService<VoiceCommandDispatcher>();

var app = builder.Build();

await app.RunServerAsync("http://0.0.0.0:8080");
