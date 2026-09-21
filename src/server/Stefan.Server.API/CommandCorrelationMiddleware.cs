using Serilog.Context;
using Stefan.Server.Common;

namespace Stefan.Server.API;

public static class CommandCorrelationMiddleware
{
    /// <summary>
    /// Reads the <see cref="Correlation.CommandIdHeader"/> header and pushes it into the Serilog
    /// log context, so all log events emitted while handling the request are correlated with the
    /// command that originated on the node.
    /// </summary>
    public static IApplicationBuilder UseCommandCorrelation(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var commandId = context.Request.Headers[Correlation.CommandIdHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(commandId))
            {
                await next(context);
                return;
            }

            using (LogContext.PushProperty(Correlation.CommandIdProperty, commandId))
            {
                await next(context);
            }
        });
    }
}
