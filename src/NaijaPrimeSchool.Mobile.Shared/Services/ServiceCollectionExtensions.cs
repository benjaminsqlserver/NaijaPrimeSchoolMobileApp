using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NaijaPrimeSchool.Mobile.Shared.Services.Api;
using NaijaPrimeSchool.Mobile.Shared.Services.Auth;
using Radzen;

namespace NaijaPrimeSchool.Mobile.Shared.Services;

public static class ServiceCollectionExtensions
{
    // Registers everything the shared UI needs. The host must also register
    // ISecureStore and IPlatformService (see Services/Platform).
    public static IServiceCollection AddNaijaPrimeSchoolClient(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddSingleton(new ApiSettings(apiBaseUrl));

        // One HttpClient for the API. Scoped services below so each Blazor
        // Server circuit (web host) gets its own session; in MAUI there is
        // a single scope for the app's lifetime.
        services.AddScoped(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        services.AddScoped<SessionManager>();
        services.AddScoped<ApiAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();

        services.AddScoped<ApiClient>();
        services.AddScoped<LookupClient>();
        services.AddScoped<AcademicClient>();
        services.AddScoped<AttendanceClient>();
        services.AddScoped<ResultsClient>();
        services.AddScoped<FamilyClient>();
        services.AddScoped<FinanceClient>();
        services.AddScoped<StoreClient>();
        services.AddScoped<CommsClient>();
        services.AddScoped<PortalClient>();
        services.AddScoped<UserClient>();
        services.AddScoped<AccountClient>();
        services.AddScoped<AppState>();

        services.AddRadzenComponents();
        return services;
    }
}
