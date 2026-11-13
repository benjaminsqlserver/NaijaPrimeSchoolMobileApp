using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Maps the Application layer's OperationResult onto HTTP. Business-rule
// failures (e.g. "cannot delete a parent who still has linked pupils")
// become 400 with the same messages the web app shows.
internal static class ApiResults
{
    public static IResult ToHttp(this OperationResult result) =>
        result.Succeeded ? Results.NoContent() : Failure(result.Errors);

    public static IResult ToHttp<T>(this OperationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Data) : Failure(result.Errors);

    public static IResult ToIdHttp(this OperationResult<Guid> result) =>
        result.Succeeded ? Results.Ok(new IdResponse { Id = result.Data }) : Failure(result.Errors);

    public static IResult OkOrNotFound<T>(T? value) =>
        value is null ? Results.NotFound() : Results.Ok(value);

    public static IResult Failure(IEnumerable<string> errors, string title = "Request failed") =>
        Results.BadRequest(new ApiError { Title = title, Errors = errors.ToList() });

    public static IResult Failure(string error) => Failure([error]);

    public static RouteHandlerBuilder RequirePolicy(this RouteHandlerBuilder builder, string policy) =>
        builder.RequireAuthorization(policy);
}
