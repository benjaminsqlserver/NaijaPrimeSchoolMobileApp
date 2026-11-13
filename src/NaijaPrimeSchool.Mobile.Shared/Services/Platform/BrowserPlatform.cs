using Microsoft.JSInterop;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Platform;

// Browser implementations, used by the Blazor Web (Auto) host in both its
// interactive-server and WebAssembly phases. Prerendering is switched off in
// that host, so JS interop is always available here.
public sealed class BrowserSecureStore(IJSRuntime js) : ISecureStore
{
    public async Task<string?> GetAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async Task SetAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async Task RemoveAsync(string key) =>
        await js.InvokeVoidAsync("localStorage.removeItem", key);
}

public sealed class BrowserPlatformService(IJSRuntime js) : IPlatformService
{
    public string PlatformName => "Browser";
    public string DeviceName => "Web browser";
    public bool CanChangeServer => false;

    public async Task OpenExternalAsync(string url) =>
        await js.InvokeVoidAsync("open", url, "_blank", "noopener");
}
