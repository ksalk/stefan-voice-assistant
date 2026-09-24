using Microsoft.Extensions.DependencyInjection;
using Stefan.Server.Application.Nodes.Jobs;

namespace Stefan.Server.Application.Nodes;

public static class NodeFeature
{
    public static IServiceCollection AddNodeFeatures(this IServiceCollection services)
    {
        services.AddScoped<RegisterNode>();
        services.AddScoped<GetNodes>();
        services.AddScoped<GetNodeDetails>();
        services.AddScoped<PingNode>();
        services.AddScoped<ScheduleNodePing>();
        services.AddScoped<RescheduleNodePings>();

        services.AddScoped<PingNodeJob>();

        services.AddScoped<SendNodeAudioMessage>();
        return services;
    }
}
