namespace Stefan.Server.Application;

public enum ErrorKind
{
    Validation,
    NotFound,
    Unauthorized,
    Conflict,
    External,
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error Validation(string message) => new(ErrorKind.Validation, message);
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);
    public static Error Unauthorized(string message) => new(ErrorKind.Unauthorized, message);
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
    public static Error External(string message) => new(ErrorKind.External, message);
}
