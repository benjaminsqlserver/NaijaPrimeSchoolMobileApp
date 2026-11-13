using NaijaPrimeSchool.Mobile.Shared.Services.Platform;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Api;

// Address of NaijaPrimeSchool.Mobile.Api. Each host supplies the default
// from its configuration; on devices the user can point the app at another
// server (e.g. the school's LAN address) from the sign-in screen.
public sealed class ApiSettings(string defaultBaseUrl)
{
    private const string OverrideKey = "nps.api_base_url";
    private string? overrideUrl;
    private bool loaded;

    public string DefaultBaseUrl { get; } = Normalize(defaultBaseUrl);

    public string BaseUrl => overrideUrl ?? DefaultBaseUrl;

    public async Task LoadAsync(ISecureStore store)
    {
        if (loaded) return;
        loaded = true;
        try
        {
            var saved = await store.GetAsync(OverrideKey);
            if (!string.IsNullOrWhiteSpace(saved)) overrideUrl = Normalize(saved);
        }
        catch
        {
            // Store not available yet: keep the default.
        }
    }

    public async Task SetOverrideAsync(ISecureStore store, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || Normalize(url) == DefaultBaseUrl)
        {
            overrideUrl = null;
            await store.RemoveAsync(OverrideKey);
            return;
        }
        overrideUrl = Normalize(url);
        await store.SetAsync(OverrideKey, overrideUrl);
    }

    public Uri Resolve(string relative) => new(new Uri(BaseUrl + "/"), relative.TrimStart('/'));

    public static bool IsValid(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static string Normalize(string url) => url.Trim().TrimEnd('/');
}
