using System.ComponentModel.DataAnnotations;

namespace NaijaPrimeSchool.Mobile.Contracts.Auth;

// Same rule as the web sign-in page: the user may type either their
// username or their email address.
public class LoginRequest
{
    [Required(ErrorMessage = "Enter your email or username")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password")]
    public string Password { get; set; } = string.Empty;

    // Identifies the installation so each device keeps its own refresh
    // token: signing in on a phone does not sign the tablet out.
    [Required, StringLength(64, MinimumLength = 8)]
    public string DeviceId { get; set; } = string.Empty;

    [StringLength(100)]
    public string? DeviceName { get; set; }
}

public class RefreshTokenRequest
{
    [Required] public Guid UserId { get; set; }
    [Required] public string RefreshToken { get; set; } = string.Empty;
    [Required] public string DeviceId { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required] public string DeviceId { get; set; } = string.Empty;
}

public class AuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresOn { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresOn { get; set; }
    public CurrentUserDto User { get; set; } = new();
}

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Enter your current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password"), StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// Custom JWT claim names. Standard ones (name identifier, name, role) use
// the System.Security.Claims.ClaimTypes URIs so ClaimsPrincipal.IsInRole
// works unchanged on both the API and the clients.
public static class MobileClaimTypes
{
    public const string FullName = "nps:full_name";
    public const string SecurityStamp = "nps:stamp";
    public const string DeviceId = "nps:device";
}
