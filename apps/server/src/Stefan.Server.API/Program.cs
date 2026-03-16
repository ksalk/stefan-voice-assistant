using Microsoft.EntityFrameworkCore;
using Stefan.Server.AI;
using Stefan.Server.AI.Tools.Timer;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.Services;
using Stefan.Server.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddDbContext<TimerDbContext>(o =>
    o.UseSqlite(configuration.GetConnectionString("TimerDb")));
builder.Services.AddApplication(configuration);
builder.Services.AddAIServices(configuration);
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
