using Microsoft.EntityFrameworkCore;
using Stefan.Server.AI;
using Stefan.Server.AI.Tools.Timer;
using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.Nodes.Scheduling;
using Stefan.Server.Application.Services;
using Stefan.Server.Common;
using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;
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

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicy.NodePolicy, policy =>
    {
        policy.RequireAssertion(context =>
        {
            var httpContext = context.Resource as HttpContext;
            if (httpContext == null)
                return false;   

            var expectedSecret = configuration["NodeSecret"];
            var providedSecret = httpContext.Request.Headers["X-Node-Secret"].FirstOrDefault();

            if (string.IsNullOrEmpty(expectedSecret) || providedSecret != expectedSecret)
            {
                ConsoleLog.Write(LogCategory.HTTP, "Unauthorized request: invalid or missing X-Node-Secret");
                return false;
            }
            return true;
        });
    });

var app = builder.Build();

// app.UseWebSockets();

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
