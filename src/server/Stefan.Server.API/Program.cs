using Stefan.Server.API;
using Stefan.Server.API.Endpoints;
using Stefan.Server.Application;
using Stefan.Server.Application.Nodes;
using Stefan.Server.Application.Services;
using Stefan.Server.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddOpenApi();

builder.Services.AddApplication(configuration);
builder.Services.AddInfrastructure(configuration);
builder.Services.AddAuth();
builder.Services.AddCors(configuration);

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
