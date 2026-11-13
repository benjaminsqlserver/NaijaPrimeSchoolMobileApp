namespace NaijaPrimeSchool.Mobile.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NaijaPrimeSchool.Mobile.Api";
    public string Audience { get; set; } = "NaijaPrimeSchool.Mobile";

    // HMAC-SHA256 key, at least 32 characters. Keep it out of source control
    // (user-secrets in development, an environment variable / key vault in
    // production: Jwt__SigningKey).
    public string SigningKey { get; set; } = string.Empty;

    // Short-lived access token; the client silently refreshes it.
    public int AccessTokenMinutes { get; set; } = 30;

    // Matches the web app's 8-hour sliding cookie in spirit but lasts longer
    // because typing a password on a phone is a bigger chore; every refresh
    // rotates the token, and deactivating the user or changing their
    // password (security stamp) invalidates it immediately.
    public int RefreshTokenDays { get; set; } = 14;

    // How long the "is this user still active / unchanged?" check may be
    // cached per user. The web app revalidates its cookie every 30 minutes;
    // the API does it on every request with a one-minute cache.
    public int RevalidateSeconds { get; set; } = 60;
}
