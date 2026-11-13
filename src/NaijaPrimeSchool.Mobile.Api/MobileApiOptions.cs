namespace NaijaPrimeSchool.Mobile.Api;

public sealed class MobileApiOptions
{
    public const string SectionName = "MobileApi";

    // Public https address of this API (e.g. https://mobile-api.naijaprimeschool.ng).
    // Used to build the payment-gateway return link; falls back to the
    // incoming request's host when empty.
    public string? PublicBaseUrl { get; set; }

    // Where relative media paths (pupil photos) are served from: the web app.
    public string? MediaBaseUrl { get; set; }

    // Link shown in the app for desktop-only tasks.
    public string? WebPortalUrl { get; set; }

    public string SchoolName { get; set; } = "Naija Prime School";

    // Browser origins allowed to call the API (the Blazor Web/Auto host used
    // on Linux and in browsers). Native MAUI apps don't need CORS.
    public string[] AllowedOrigins { get; set; } = [];

    // The web app already runs the email / SMS dispatcher against the shared
    // database. Leave this false so two processes don't both pick up the
    // same notification rows; set it true only if the API is deployed
    // without the web app.
    public bool RunNotificationDispatcher { get; set; }
}
