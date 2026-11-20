namespace Stefan.Server.Application;

public enum ErrorKind
{
    Validation,
    NotFound,
    Unauthorized,
    Conflict,
    External,
    NoSpeech,
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error Validation(string message) => new(ErrorKind.Validation, message);
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);
    public static Error Unauthorized(string message) => new(ErrorKind.Unauthorized, message);
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
    public static Error External(string message) => new(ErrorKind.External, message);
    public static Error NoSpeech(string message) => new(ErrorKind.NoSpeech, message);
}
