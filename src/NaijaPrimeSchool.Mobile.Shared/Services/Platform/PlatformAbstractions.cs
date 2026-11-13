namespace NaijaPrimeSchool.Mobile.Shared.Services.Platform;

// Where the session (tokens) and small settings are kept. MAUI uses the OS
// keychain / keystore (SecureStorage); the browser host uses localStorage.
public interface ISecureStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}

// Things only the host can do.
public interface IPlatformService
{
    // "Android", "iOS", "MacCatalyst", "Windows", "Browser".
    string PlatformName { get; }

    // Friendly device description sent at sign-in (shown in logs only).
    string DeviceName { get; }

    // Opens a link outside the app (payment checkout, the web portal).
    Task OpenExternalAsync(string url);

    // True when the host lets the user change the API address at runtime
    // (device builds). The browser host always uses its configured API.
    bool CanChangeServer { get; }
}

// Fallback store used if the platform store is unavailable (for example
// SecureStorage on an unpackaged Windows build without credentials).
public sealed class InMemorySecureStore : ISecureStore
{
    private readonly Dictionary<string, string> values = new();

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(values.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value)
    {
        values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        values.Remove(key);
        return Task.CompletedTask;
    }
}
