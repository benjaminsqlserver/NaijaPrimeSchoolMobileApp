using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NaijaPrimeSchool.Mobile.Shared.Services;
using NaijaPrimeSchool.Mobile.Shared.Services.Platform;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same registrations as the server half (NaijaPrimeSchool.Mobile.Web), so a
// page behaves identically whether Auto mode is running it on the server or
// in WebAssembly. The session lives in localStorage and is shared by both.
builder.Services.AddScoped<ISecureStore, BrowserSecureStore>();
builder.Services.AddScoped<IPlatformService, BrowserPlatformService>();
builder.Services.AddNaijaPrimeSchoolClient(
    builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7280");

await builder.Build().RunAsync();
