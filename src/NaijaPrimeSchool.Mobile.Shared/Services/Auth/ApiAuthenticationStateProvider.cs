using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NaijaPrimeSchool.Mobile.Contracts.Auth;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Auth;

// Builds the Blazor AuthenticationState from the stored session so the
// usual <AuthorizeView Roles="..."> and [Authorize(Roles = ...)] work in the
// shared UI. The API re-checks every request; this is only for the UI.
public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly SessionManager sessions;

    public ApiAuthenticationStateProvider(SessionManager sessions)
    {
        this.sessions = sessions;
        sessions.SessionChanged += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var session = await sessions.GetSessionAsync();
            return session is null ? Anonymous : new AuthenticationState(ToPrincipal(session.User));
        }
        catch
        {
            // Storage not reachable (e.g. JS interop not ready): treat as signed out.
            return Anonymous;
        }
    }

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static ClaimsPrincipal ToPrincipal(CurrentUserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(MobileClaimTypes.FullName, user.FullName),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "NaijaPrimeSchool.Mobile.Api", ClaimTypes.Name, ClaimTypes.Role));
    }

    public void Dispose() => sessions.SessionChanged -= OnSessionChanged;
}
