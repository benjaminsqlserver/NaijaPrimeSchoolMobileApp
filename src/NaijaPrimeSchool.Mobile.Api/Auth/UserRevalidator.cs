using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaijaPrimeSchool.Infrastructure.Persistence;
using NaijaPrimeSchool.Mobile.Contracts.Auth;

namespace NaijaPrimeSchool.Mobile.Api.Auth;

// Mobile counterpart of the web app's IdentityRevalidatingAuthenticationStateProvider:
// a token is only honoured while its user still exists, is active, is not
// soft-deleted, and has the same security stamp it was issued with (a
// password reset or role change on the web rotates the stamp). Results are
// cached for a short time so this is not a database hit per request.
public sealed class UserRevalidator(
    ApplicationDbContext db,
    IMemoryCache cache,
    IOptions<JwtOptions> options)
{
    private sealed record UserState(bool IsActive, bool IsDeleted, string? SecurityStamp);

    public async Task<bool> IsStillValidAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return false;
        var stamp = principal.FindFirstValue(MobileClaimTypes.SecurityStamp);

        var state = await cache.GetOrCreateAsync($"nps-user-state:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, options.Value.RevalidateSeconds));
            return await db.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserState(u.IsActive, u.IsDeleted, u.SecurityStamp))
                .FirstOrDefaultAsync(ct);
        });

        return state is { IsActive: true, IsDeleted: false }
               && string.Equals(state.SecurityStamp, stamp, StringComparison.Ordinal);
    }

    public void Forget(Guid userId) => cache.Remove($"nps-user-state:{userId}");
}
