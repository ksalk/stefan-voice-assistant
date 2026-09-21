namespace Stefan.Node;

/// <summary>
/// Names used to correlate a single voice command across the node, the HTTP boundary and the server logs.
/// </summary>
public static class Correlation
{
    /// <summary>Request/response header carrying the node generated command id.</summary>
    public const string CommandIdHeader = "X-Command-ID";

    /// <summary>Log property (and log scope key) used for the command id.</summary>
    public const string CommandIdProperty = "CommandId";
}
