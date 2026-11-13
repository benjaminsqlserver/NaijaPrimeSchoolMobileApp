using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace NaijaPrimeSchool.Mobile.Web;

// The page shell is public; authorisation happens in the browser (the
// AuthorizeRouteView redirects to /login) and, authoritatively, in the
// Mobile API on every data request.
internal sealed class ClientSideAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult) =>
        next(context);
}
