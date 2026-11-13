using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NaijaPrimeSchool.Mobile.Contracts.Auth;
using NaijaPrimeSchool.Mobile.Contracts.Common;
using NaijaPrimeSchool.Mobile.Shared.Services.Api;
using NaijaPrimeSchool.Mobile.Shared.Services.Platform;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Auth;

public sealed class AuthSession
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresOn { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresOn { get; set; }
    public CurrentUserDto User { get; set; } = new();
}

public sealed record SignInResult(bool Succeeded, string? Error = null)
{
    public static SignInResult Ok() => new(true);
    public static SignInResult Fail(string error) => new(false, error);
}

// Owns the signed-in session: stores the tokens securely, refreshes the
// access token shortly before it expires (single-flight), and announces
// sign-in / sign-out so the UI's AuthenticationState follows along.
public sealed class SessionManager(
    HttpClient http,
    ApiSettings settings,
    ISecureStore store,
    IPlatformService platform)
{
    private const string SessionKey = "nps.session";
    private const string DeviceKey = "nps.device_id";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim gate = new(1, 1);
    private AuthSession? session;
    private bool loaded;

    public event Action? SessionChanged;

    public CurrentUserDto? CurrentUser => session?.User;

    public async Task<AuthSession?> GetSessionAsync()
    {
        if (loaded) return session;
        await gate.WaitAsync();
        try
        {
            if (!loaded)
            {
                await settings.LoadAsync(store);
                var raw = await SafeGetAsync(SessionKey);
                session = string.IsNullOrEmpty(raw) ? null : JsonSerializer.Deserialize<AuthSession>(raw, Json);
                if (session is not null && session.RefreshTokenExpiresOn <= DateTimeOffset.UtcNow)
                {
                    session = null;
                    await SafeRemoveAsync(SessionKey);
                }
                loaded = true;
            }
        }
        catch (JsonException)
        {
            session = null;
            loaded = true;
        }
        finally
        {
            gate.Release();
        }
        return session;
    }

    public async Task<SignInResult> SignInAsync(string userNameOrEmail, string password)
    {
        await settings.LoadAsync(store);
        try
        {
            var response = await http.PostAsJsonAsync(settings.Resolve("api/auth/login"), new LoginRequest
            {
                UserNameOrEmail = userNameOrEmail,
                Password = password,
                DeviceId = await GetDeviceIdAsync(),
                DeviceName = platform.DeviceName,
            }, Json);

            if (response.IsSuccessStatusCode)
            {
                var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(Json);
                if (tokens is null) return SignInResult.Fail("Unexpected response from the server.");
                await ApplyAsync(tokens);
                return SignInResult.Ok();
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return SignInResult.Fail("Too many attempts. Please wait a minute and try again.");

            var error = await TryReadErrorAsync(response);
            return SignInResult.Fail(error ?? "Invalid sign in attempt.");
        }
        catch (HttpRequestException)
        {
            return SignInResult.Fail($"Cannot reach the school server at {settings.BaseUrl}. Check your connection.");
        }
        catch (TaskCanceledException)
        {
            return SignInResult.Fail("The school server took too long to answer. Please try again.");
        }
    }

    // Returns a usable access token, refreshing it first when it is about to
    // expire (or when forceRefresh is set after a 401). Null means the
    // session is over and the user must sign in again.
    public async Task<string?> GetAccessTokenAsync(bool forceRefresh = false)
    {
        var current = await GetSessionAsync();
        if (current is null) return null;

        if (!forceRefresh && current.AccessTokenExpiresOn > DateTimeOffset.UtcNow.AddSeconds(60))
            return current.AccessToken;

        await gate.WaitAsync();
        try
        {
            // Another caller may have refreshed while we waited.
            if (session is null) return null;
            if (!ReferenceEquals(session, current) && session.AccessTokenExpiresOn > DateTimeOffset.UtcNow.AddSeconds(60))
                return session.AccessToken;

            var response = await http.PostAsJsonAsync(settings.Resolve("api/auth/refresh"), new RefreshTokenRequest
            {
                UserId = session.User.Id,
                RefreshToken = session.RefreshToken,
                DeviceId = await GetDeviceIdAsync(),
            }, Json);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                await ClearAsync();
                return null;
            }
            response.EnsureSuccessStatusCode();

            var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(Json);
            if (tokens is null)
            {
                await ClearAsync();
                return null;
            }
            session = ToSession(tokens);
            await store.SetAsync(SessionKey, JsonSerializer.Serialize(session, Json));
            return session.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SignOutAsync()
    {
        var current = await GetSessionAsync();
        if (current is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, settings.Resolve("api/auth/logout"))
                {
                    Content = JsonContent.Create(new LogoutRequest { DeviceId = await GetDeviceIdAsync() }, options: Json),
                };
                request.Headers.Authorization = new("Bearer", current.AccessToken);
                await http.SendAsync(request);
            }
            catch
            {
                // Signing out locally must always work, even offline.
            }
        }
        await ClearAsync();
    }

    // Used after a password change: the API hands back a fresh token pair.
    public async Task ApplyAsync(AuthTokenResponse tokens)
    {
        session = ToSession(tokens);
        loaded = true;
        await store.SetAsync(SessionKey, JsonSerializer.Serialize(session, Json));
        SessionChanged?.Invoke();
    }

    // Called when the API rejects the session (deactivated user, password
    // changed elsewhere, refresh token expired).
    public async Task ClearAsync()
    {
        var had = session is not null;
        session = null;
        loaded = true;
        await SafeRemoveAsync(SessionKey);
        if (had) SessionChanged?.Invoke();
    }

    private async Task<string> GetDeviceIdAsync()
    {
        var id = await SafeGetAsync(DeviceKey);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            await store.SetAsync(DeviceKey, id);
        }
        return id;
    }

    private static AuthSession ToSession(AuthTokenResponse t) => new()
    {
        AccessToken = t.AccessToken,
        AccessTokenExpiresOn = t.AccessTokenExpiresOn,
        RefreshToken = t.RefreshToken,
        RefreshTokenExpiresOn = t.RefreshTokenExpiresOn,
        User = t.User,
    };

    private async Task<string?> SafeGetAsync(string key)
    {
        try { return await store.GetAsync(key); }
        catch { return null; }
    }

    private async Task SafeRemoveAsync(string key)
    {
        try { await store.RemoveAsync(key); }
        catch { /* ignore */ }
    }

    internal static async Task<string?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json);
            return error?.Errors.Count > 0 ? string.Join(" ", error.Errors) : null;
        }
        catch
        {
            return null;
        }
    }
}
