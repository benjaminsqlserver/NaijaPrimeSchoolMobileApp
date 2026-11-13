using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NaijaPrimeSchool.Mobile.Services;
using NaijaPrimeSchool.Mobile.Shared.Services;
using NaijaPrimeSchool.Mobile.Shared.Services.Platform;

namespace NaijaPrimeSchool.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Platform services for the shared UI.
        builder.Services.AddSingleton<ISecureStore, MauiSecureStore>();
        builder.Services.AddSingleton<IPlatformService, MauiPlatformService>();

        // Same UI and client stack as the web host; only the API address and
        // the platform services differ.
        builder.Services.AddNaijaPrimeSchoolClient(ResolveApiBaseUrl());

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static string ResolveApiBaseUrl()
    {
        var url = "http://localhost:5280";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NaijaPrimeSchool.Mobile.appsettings.json"))
        {
            if (stream is not null)
            {
                using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("Api", out var api)
                    && api.TryGetProperty("BaseUrl", out var baseUrl)
                    && baseUrl.GetString() is { Length: > 0 } configured)
                {
                    url = configured;
                }
            }
        }

#if ANDROID
        // The Android emulator reaches the development machine at 10.0.2.2.
        if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
            url = url.Replace("://localhost", "://10.0.2.2").Replace("://127.0.0.1", "://10.0.2.2");
#endif
        return url;
    }
}
