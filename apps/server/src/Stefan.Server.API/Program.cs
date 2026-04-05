using Microsoft.EntityFrameworkCore;
using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.AI.Tools.Timer;
using Stefan.Server.Application.Nodes.Scheduling;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;
using Stefan.Server.Infrastructure.Authentication;
using Stefan.Server.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddApplication(configuration);
builder.Services.AddInfrastructure(configuration);
// builder.Services.AddSingleton<NodeRegistry>();
// builder.Services.AddSingleton<NodeWebSocketHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(NodeSecretAuthenticationOptions.DefaultScheme)
    .AddScheme<NodeSecretAuthenticationOptions, NodeSecretAuthenticationHandler>(
        NodeSecretAuthenticationOptions.DefaultScheme,
        options => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicy.NodePolicy, policy =>
    {
        policy.AddAuthenticationSchemes(NodeSecretAuthenticationOptions.DefaultScheme);
        policy.RequireAuthenticatedUser();
    });

var app = builder.Build();

// app.UseWebSockets();
app.UseCors();
app.UseAuthentication();

// Ensure the SQLite database and schema exist on startup.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<TimerDbContext>().Database.EnsureCreated();

// Reschedule ping jobs for all online nodes after server restart
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<StefanDbContext>();
    var pingScheduler = services.GetRequiredService<INodePingScheduler>();

    // Clear any orphaned jobs first, then reschedule online nodes
    await pingScheduler.RescheduleAllOnlineNodesAsync();

    var onlineNodes = await dbContext.Nodes
        .Where(n => n.Status == NodeStatus.Online)
        .ToListAsync();

    foreach (var node in onlineNodes)
    {
        await pingScheduler.SchedulePingAsync(node.Id);
    }
}

// Eagerly load the STT model so it's ready before the first request.
app.Services.GetRequiredService<ISpeechToTextService>();

// Eagerly load the TTS engine (downloads piper/model if missing) so it's ready before the first request.
app.Services.GetRequiredService<TextToSpeechService>();

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
