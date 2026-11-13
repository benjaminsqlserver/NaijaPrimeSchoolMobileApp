using System.Security.Claims;
using NaijaPrimeSchool.Application.Common;

namespace NaijaPrimeSchool.Mobile.Api.Auth;

// ICurrentUser for the API: same contract as the web app's
// CurrentUserAccessor, fed from the validated JWT instead of the Identity
// cookie. The DbContext uses it to stamp CreatedBy / ModifiedBy / DeletedBy
// and the audit trail, so changes made from a phone are attributed exactly
// like changes made on the web.
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
