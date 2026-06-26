using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Services;
using Stefan.Server.Infrastructure.Authentication;
using Stefan.Server.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddApplication(configuration);
builder.Services.AddInfrastructure(configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication()
    .AddScheme<NodeSecretAuthenticationOptions, NodeSecretAuthenticationHandler>(
        NodeSecretAuthenticationOptions.DefaultScheme,
        options => { })
    .AddScheme<DashboardAuthenticationOptions, DashboardAuthenticationHandler>(
        DashboardAuthenticationOptions.DefaultScheme,
        options => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicy.NodePolicy, policy =>
    {
        policy.AddAuthenticationSchemes(NodeSecretAuthenticationOptions.DefaultScheme);
        policy.RequireAuthenticatedUser();
    })
    .AddPolicy(AuthPolicy.DashboardPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(DashboardAuthenticationOptions.DefaultScheme);
        policy.RequireAuthenticatedUser();
    });

var app = builder.Build();

// app.UseWebSockets();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Reschedule ping jobs for all online nodes after server restart
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var rescheduleNodePings = services.GetRequiredService<RescheduleNodePings>();
    await rescheduleNodePings.Handle(CancellationToken.None);
}

// Eagerly load the STT model so it's ready before the first request.
app.Services.GetRequiredService<ISpeechToTextService>();

// Eagerly load the TTS engine (downloads piper/model if missing) so it's ready before the first request.
app.Services.GetRequiredService<ITextToSpeechService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapNodeEndpoints();
app.MapCommandEndpoints();

// app.Map("/ws", (HttpContext context, NodeWebSocketHandler wsHandler, CancellationToken cancellationToken) =>
//     wsHandler.HandleAsync(context, cancellationToken));


app.Run();
