using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

/// <summary>Creates the wake-word detector selected by <see cref="WakeWordOptions"/>.</summary>
public static class WakeWordDetectorFactory
{
    public static IWakeWordDetector Create(IServiceProvider services)
    {
        var detector = WakeWordOptions.Normalize(
            services.GetRequiredService<IOptions<WakeWordOptions>>().Value.Detector);

        return detector switch
        {
            WakeWordOptions.NanoWakeWordDetector => CreateNanoWakeWordDetector(services),
            WakeWordOptions.SherpaDetector => CreateSherpaDetector(services),
            _ => throw new InvalidOperationException($"Unsupported wake-word detector '{detector}'.")
        };
    }

    private static NanoWakeWordDetector CreateNanoWakeWordDetector(IServiceProvider services) =>
        new(
            services.GetRequiredService<IOptions<NanoWakeWordOptions>>(),
            services.GetRequiredService<ILogger<NanoWakeWordDetector>>());

    private static SherpaWakeWordDetector CreateSherpaDetector(IServiceProvider services) =>
        new(
            services.GetRequiredService<IOptions<KeywordSpotterOptions>>(),
            services.GetRequiredService<IOptions<AudioOptions>>());
}
