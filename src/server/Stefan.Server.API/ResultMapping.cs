using Microsoft.AspNetCore.Http;
using Stefan.Server.Application;

namespace Stefan.Server.API;

public static class ResultMapping
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, error => error.ToHttpResult());

    public static IResult ToHttpResult(this Error error) =>
        error.Kind switch
        {
            ErrorKind.Validation => Results.BadRequest(error.Message),
            ErrorKind.NotFound => Results.NotFound(error.Message),
            ErrorKind.Unauthorized => Results.Unauthorized(),
            ErrorKind.Conflict => Results.Conflict(error.Message),
            _ => Results.Problem(error.Message),
        };
}
