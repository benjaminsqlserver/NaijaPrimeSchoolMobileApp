using NaijaPrimeSchool.Application.Users;
using NaijaPrimeSchool.Application.Users.Dtos;
using NaijaPrimeSchool.Mobile.Api.Auth;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// SuperAdmin user management: look someone up, deactivate / reactivate an
// account. Deactivation takes effect on mobile within the revalidation
// window because UserRevalidator re-reads IsActive.
internal static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.UserAdmins);

        group.MapGet("", (QueryFilter<UserListFilter> query, IUserService s, CancellationToken ct) =>
        {
            query.Value.Take = Math.Clamp(query.Value.Take, 1, 100);
            return s.ListAsync(query.Value, ct);
        });
        group.MapGet("/{id:guid}", async (Guid id, IUserService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        group.MapPost("/{id:guid}/active", async (Guid id, SetActiveRequest body, IUserService s, UserRevalidator revalidator, CancellationToken ct) =>
        {
            var result = await s.SetActiveAsync(id, body.IsActive, body.Reason, ct);
            revalidator.Forget(id);
            return result.ToHttp();
        });
    }
}
