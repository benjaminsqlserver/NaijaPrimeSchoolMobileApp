using NaijaPrimeSchool.Mobile.Shared.Services.Platform;

namespace NaijaPrimeSchool.Mobile.Services;

// Tokens go into the OS secure store: Keychain (iOS / macOS), Android
// Keystore-backed EncryptedSharedPreferences, Windows DPAPI.
public sealed class MauiSecureStore : ISecureStore
{
    private readonly InMemorySecureStore fallback = new();
    private bool useFallback;

    public async Task<string?> GetAsync(string key)
    {
        if (useFallback) return await fallback.GetAsync(key);
        try { return await SecureStorage.Default.GetAsync(key); }
        catch (Exception) { useFallback = true; return await fallback.GetAsync(key); }
    }

    public async Task SetAsync(string key, string value)
    {
        if (useFallback) { await fallback.SetAsync(key, value); return; }
        try { await SecureStorage.Default.SetAsync(key, value); }
        catch (Exception)
        {
            // E.g. an unpackaged Windows build without a package identity.
            // The session then lasts until the app closes.
            useFallback = true;
            await fallback.SetAsync(key, value);
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (useFallback) { await fallback.RemoveAsync(key); return; }
        try { SecureStorage.Default.Remove(key); }
        catch (Exception) { await fallback.RemoveAsync(key); }
    }
}

public sealed class MauiPlatformService : IPlatformService
{
    public string PlatformName => DeviceInfo.Current.Platform.ToString();

    public string DeviceName
    {
        get
        {
            var name = $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model} ({DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString})".Trim();
            return name.Length > 100 ? name[..100] : name;
        }
    }

    public bool CanChangeServer => true;

    public Task OpenExternalAsync(string url) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.SystemPreferred));
}
