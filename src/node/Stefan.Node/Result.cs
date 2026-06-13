namespace Stefan.Node;

public readonly record struct Result
{
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(string? error)
    {
        Error = error;
    }

    public static Result Success() => new(null);
    public static Result Failure(string error) => new(error);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<string, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error!);
}

public readonly record struct Result<T>
{
    public T Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value, string? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default!, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error!);
}
