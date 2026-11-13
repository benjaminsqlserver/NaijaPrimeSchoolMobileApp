using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NaijaPrimeSchool.Domain.Identity;
using NaijaPrimeSchool.Infrastructure.Persistence;
using NaijaPrimeSchool.Mobile.Contracts.Auth;

namespace NaijaPrimeSchool.Mobile.Api.Auth;

// Issues access + refresh tokens for users of the shared Identity store.
//
// Refresh tokens live in the existing Identity "UserTokens" table (one row
// per user per device, LoginProvider = "NaijaPrimeSchool.Mobile"), so the
// mobile app needs no schema change in the web app's database. Only a
// SHA-256 hash of the token is stored, and the audit trail already excludes
// that table because it holds secrets.
public sealed class TokenService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> options,
    TimeProvider time)
{
    public const string LoginProvider = "NaijaPrimeSchool.Mobile";

    private JwtOptions Options => options.Value;

    public static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    public async Task<AuthTokenResponse> IssueAsync(ApplicationUser user, string deviceId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var stamp = await userManager.GetSecurityStampAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(MobileClaimTypes.FullName, user.FullName),
            new(MobileClaimTypes.SecurityStamp, stamp),
            new(MobileClaimTypes.DeviceId, deviceId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var accessExpires = now.AddMinutes(Options.AccessTokenMinutes);
        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Options.Issuer,
            Audience = Options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(Options), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var refreshExpires = now.AddDays(Options.RefreshTokenDays);
        await StoreRefreshTokenAsync(user.Id, deviceId, refreshToken, refreshExpires, ct);

        return new AuthTokenResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresOn = accessExpires,
            RefreshToken = refreshToken,
            RefreshTokenExpiresOn = refreshExpires,
            User = new CurrentUserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.OrderBy(r => r).ToList(),
            },
        };
    }

    // True when the presented token matches the one stored for this user and
    // device and has not expired. A token is single-use: callers re-issue
    // (rotate) straight after a successful check.
    public async Task<bool> ValidateRefreshTokenAsync(Guid userId, string deviceId, string refreshToken, CancellationToken ct)
    {
        var row = await FindRowAsync(userId, deviceId, ct);
        if (row?.Value is not { } stored) return false;

        var parts = stored.Split('.', 2);
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix))
            return false;

        if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix) <= time.GetUtcNow()) return false;

        var expected = Convert.FromBase64String(parts[1]);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public async Task RevokeAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        var row = await FindRowAsync(userId, deviceId, ct);
        if (row is null) return;
        db.Set<IdentityUserToken<Guid>>().Remove(row);
        await db.SaveChangesAsync(ct);
    }

    // Signs the user out on every device (used after a password change).
    public async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Set<IdentityUserToken<Guid>>()
            .Where(t => t.UserId == userId && t.LoginProvider == LoginProvider)
            .ToListAsync(ct);
        if (rows.Count == 0) return;
        db.Set<IdentityUserToken<Guid>>().RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    private async Task StoreRefreshTokenAsync(Guid userId, string deviceId, string token, DateTimeOffset expires, CancellationToken ct)
    {
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var value = $"{expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}.{hash}";

        var row = await FindRowAsync(userId, deviceId, ct);
        if (row is null)
        {
            db.Set<IdentityUserToken<Guid>>().Add(new IdentityUserToken<Guid>
            {
                UserId = userId,
                LoginProvider = LoginProvider,
                Name = TokenName(deviceId),
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
        await db.SaveChangesAsync(ct);
    }

    private Task<IdentityUserToken<Guid>?> FindRowAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        var name = TokenName(deviceId);
        return db.Set<IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.LoginProvider == LoginProvider && t.Name == name, ct);
    }

    private static string TokenName(string deviceId) => $"refresh:{deviceId.Trim()}";
}
