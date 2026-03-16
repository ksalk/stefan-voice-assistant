using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Stefan.Server.AI;
using Stefan.Server.AI.Tools.Timer;
using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Common;
using Stefan.Server.Infrastructure.DependencyInjection;
using Whisper.net;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ =>
{
    var factory = WhisperFactory.FromPath("ggml-base.bin");
    return factory.CreateBuilder()
        .WithLanguage("en")
        .Build();
});
builder.Services.AddSingleton<SpeechToTextService>();
builder.Services.AddDbContext<TimerDbContext>(o =>
    o.UseSqlite(configuration.GetConnectionString("TimerDb")));
builder.Services.AddAiServices(configuration);
builder.Services.AddInfrastructure(configuration);
// builder.Services.AddSingleton<NodeRegistry>();
// builder.Services.AddSingleton<NodeWebSocketHandler>();

var app = builder.Build();

// app.UseWebSockets();

// Ensure the SQLite database and schema exist on startup.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<TimerDbContext>().Database.EnsureCreated();

// Eagerly load the STT model so it's ready before the first request.
app.Services.GetRequiredService<SpeechToTextService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapNodeEndpoints();
app.MapCommandEndpoints();

// app.Map("/ws", (HttpContext context, NodeWebSocketHandler wsHandler, CancellationToken cancellationToken) =>
//     wsHandler.HandleAsync(context, cancellationToken));


app.Run();
