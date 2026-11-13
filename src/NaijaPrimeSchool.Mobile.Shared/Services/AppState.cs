using System.Globalization;
using NaijaPrimeSchool.Mobile.Contracts.Common;
using NaijaPrimeSchool.Mobile.Shared.Services.Api;

namespace NaijaPrimeSchool.Mobile.Shared.Services;

// App-wide state: the server's public configuration and the unread badges
// shown in the navigation.
public sealed class AppState(ApiClient api, CommsClient comms)
{
    public AppConfigDto Config { get; private set; } = new();
    public bool ConfigLoaded { get; private set; }
    public int UnreadAnnouncements { get; private set; }
    public int UnreadMessages { get; private set; }

    public event Action? Changed;

    public async Task EnsureConfigAsync()
    {
        if (ConfigLoaded) return;
        try
        {
            Config = await api.GetAsync<AppConfigDto>("api/app-config", anonymous: true) ?? new();
            ConfigLoaded = true;
            Changed?.Invoke();
        }
        catch (ApiException)
        {
            // Offline: keep defaults and try again next time.
        }
    }

    public async Task RefreshBadgesAsync(bool announcements, bool messages)
    {
        try
        {
            if (announcements) UnreadAnnouncements = await comms.UnreadAnnouncementsAsync();
            if (messages) UnreadMessages = await comms.UnreadThreadsAsync();
            Changed?.Invoke();
        }
        catch (ApiException)
        {
            // Badges are best effort.
        }
    }

    public void ResetConfig() => ConfigLoaded = false;

    // Pupil photos are stored as web-app-relative paths (/uploads/students/..).
    public string? MediaUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out _)) return path;
        var root = string.IsNullOrWhiteSpace(Config.MediaBaseUrl) ? null : Config.MediaBaseUrl.TrimEnd('/');
        return root is null ? null : $"{root}/{path.TrimStart('/')}";
    }
}

public static class Format
{

    public static string Money(decimal amount) => "₦" + amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
    public static string Date(DateOnly? d) => d?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "—";
    public static string Date(DateTimeOffset? d) => d?.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "—";
    public static string DateTime(DateTimeOffset? d) => d?.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) ?? "—";
    public static string Time(TimeOnly? t) => t?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
    public static string Percent(double? p) => p is null ? "—" : $"{p.Value:0.#}%";
    public static string Percent(decimal p) => $"{p:0.#}%";
    public static string Quantity(decimal q) => q.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.UtcNow - when;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} d ago";
        return Date(when);
    }

    public static string Initials(string? first, string? last) =>
        $"{(string.IsNullOrEmpty(first) ? "" : first[..1])}{(string.IsNullOrEmpty(last) ? "" : last[..1])}".ToUpperInvariant();
}
