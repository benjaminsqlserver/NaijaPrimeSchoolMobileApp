using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using NaijaPrimeSchool.Mobile.Shared.Services;
using NaijaPrimeSchool.Mobile.Shared.Services.Platform;
using NaijaPrimeSchool.Mobile.Web;
using NaijaPrimeSchool.Mobile.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Shared UI services for the interactive-server phase of Auto mode (the
// WebAssembly phase registers the same ones in the Client project).
builder.Services.AddScoped<ISecureStore, BrowserSecureStore>();
builder.Services.AddScoped<IPlatformService, BrowserPlatformService>();
builder.Services.AddNaijaPrimeSchoolClient(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7280");

// Pages carry [Authorize] for the in-app router. The user's token lives in
// the browser, not in a cookie, so the HTTP request that loads the page
// can't be authorised here: let it through and let the router (and,
// above all, the API) enforce access.
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ClientSideAuthorizationResultHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(NaijaPrimeSchool.Mobile.Shared._Imports).Assembly,
        typeof(NaijaPrimeSchool.Mobile.Web.Client._Imports).Assembly);

app.Run();
