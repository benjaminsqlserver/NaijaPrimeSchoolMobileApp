using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NaijaPrimeSchool.Domain.Identity;
using NaijaPrimeSchool.Mobile.Api.Auth;
using NaijaPrimeSchool.Mobile.Contracts.Auth;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Sign-in against the web app's Identity store: same users, same password
// hashes, same lockout policy (5 attempts / 15 minutes), same "deactivated"
// and soft-delete rules as the web Login page.
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", MeAsync).RequireAuthorization();
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokens,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrEmpty(request.Password)
            || string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Length > 64)
            return ApiResults.Failure("Enter your email or username and password.");

        var login = request.UserNameOrEmail.Trim();
        var user = await userManager.FindByNameAsync(login) ?? await userManager.FindByEmailAsync(login);

        if (user is null || user.IsDeleted)
            return Unauthorized("Invalid sign in attempt.");

        if (!user.IsActive)
            return Unauthorized("Your account has been deactivated. Please contact your administrator.");

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Unauthorized("Account locked. Please try again later or contact your administrator.");
        if (!result.Succeeded)
            return Unauthorized("Invalid sign in attempt.");

        loggerFactory.CreateLogger("Auth").LogInformation(
            "User {User} signed in on mobile device {Device}.", user.UserName, request.DeviceName ?? request.DeviceId);

        return Results.Ok(await tokens.IssueAsync(user, request.DeviceId.Trim(), ct));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        UserManager<ApplicationUser> userManager,
        TokenService tokens,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted || !user.IsActive)
            return Unauthorized("Your session has ended. Please sign in again.");

        if (!await tokens.ValidateRefreshTokenAsync(user.Id, request.DeviceId, request.RefreshToken, ct))
            return Unauthorized("Your session has ended. Please sign in again.");

        // Rotation: the presented refresh token is replaced and can't be reused.
        return Results.Ok(await tokens.IssueAsync(user, request.DeviceId.Trim(), ct));
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request, ClaimsPrincipal principal, TokenService tokens, CancellationToken ct)
    {
        if (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            await tokens.RevokeAsync(userId, request.DeviceId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new CurrentUserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.OrderBy(r => r).ToList(),
        });
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        TokenService tokens,
        UserRevalidator revalidator,
        CancellationToken ct)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return ApiResults.Failure("Passwords do not match.");

        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return ApiResults.Failure(result.Errors.Select(e => e.Description));

        // ChangePasswordAsync rotated the security stamp, so existing access
        // tokens stop working everywhere; drop every device's refresh token
        // and hand this device a fresh pair.
        await tokens.RevokeAllAsync(user.Id, ct);
        revalidator.Forget(user.Id);
        var deviceId = principal.FindFirstValue(MobileClaimTypes.DeviceId) ?? "unknown-device";
        return Results.Ok(await tokens.IssueAsync(user, deviceId, ct));
    }

    private static IResult Unauthorized(string message) =>
        Results.Json(new NaijaPrimeSchool.Mobile.Contracts.Common.ApiError { Title = "Sign in failed", Errors = [message] },
            statusCode: StatusCodes.Status401Unauthorized);
}
