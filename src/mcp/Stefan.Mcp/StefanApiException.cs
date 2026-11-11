namespace Stefan.Mcp;

public sealed class StefanApiException(int statusCode, string title, string detail)
    : Exception($"{title} ({statusCode}): {detail}")
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}
