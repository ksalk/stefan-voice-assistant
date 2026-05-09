using Stefan.Node.HttpServer;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddHostedService<VoiceCommandDispatcher>();

var app = builder.Build();

await app.RunServerAsync("http://0.0.0.0:8080");
