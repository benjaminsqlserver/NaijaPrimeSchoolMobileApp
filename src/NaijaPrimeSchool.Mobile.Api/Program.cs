using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Infrastructure;
using NaijaPrimeSchool.Infrastructure.Notifications;
using NaijaPrimeSchool.Mobile.Api;
using NaijaPrimeSchool.Mobile.Api.Auth;
using NaijaPrimeSchool.Mobile.Api.Endpoints;
using NaijaPrimeSchool.Mobile.Contracts.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MobileApiOptions>(builder.Configuration.GetSection(MobileApiOptions.SectionName));
var mobileOptions = builder.Configuration.GetSection(MobileApiOptions.SectionName).Get<MobileApiOptions>() ?? new();

// ---- Shared core: the web app's Infrastructure (EF Core + SQL Server +
//      ASP.NET Core Identity + every Application service) ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddInfrastructure(builder.Configuration);

if (!mobileOptions.RunNotificationDispatcher)
{
    // One dispatcher per database: the web app owns sending.
    var worker = builder.Services.FirstOrDefault(d => d.ImplementationType == typeof(NotificationDispatchWorker));
    if (worker is not null) builder.Services.Remove(worker);
}

// ---- JWT bearer authentication ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:SigningKey must be set to a secret of at least 32 characters " +
        "(dotnet user-secrets set \"Jwt:SigningKey\" \"...\" or the Jwt__SigningKey environment variable).");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserRevalidator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = TokenService.CreateSigningKey(jwt),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var revalidator = context.HttpContext.RequestServices.GetRequiredService<UserRevalidator>();
                if (context.Principal is null
                    || !await revalidator.IsStillValidAsync(context.Principal, context.HttpContext.RequestAborted))
                {
                    context.Fail("The account is no longer active or its credentials have changed.");
                }
            },
        };
    });

// One policy per role group, mirroring the web pages' [Authorize(Roles=...)].
builder.Services.AddAuthorization(options =>
{
    foreach (var (policy, roles) in Policies.RoleGroupByPolicy)
        options.AddPolicy(policy, p => p.RequireAuthenticatedUser().RequireRole(RoleGroups.Split(roles)));
});

// Throttle credential endpoints per client IP (Identity lockout already
// protects each account).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (mobileOptions.AllowedOrigins.Length > 0)
        policy.WithOrigins(mobileOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Same rule as the web app: the in-memory payment simulator is never
// allowed outside development. It also cannot work from this API (its
// checkout page lives in the web app), so refuse it here altogether.
var onlinePayments = builder.Configuration.GetSection("OnlinePayments");
if (onlinePayments.GetValue<bool>("Enabled")
    && string.Equals(onlinePayments.GetValue<string>("Provider"), "Simulator", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning("OnlinePayments:Provider 'Simulator' is not supported by the mobile API; use Paystack or disable online payments.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Public start-up configuration for the clients.
app.MapGet("/api/app-config", (IOptions<MobileApiOptions> options, NaijaPrimeSchool.Application.Finance.IOnlinePaymentService online) =>
        new AppConfigDto
        {
            SchoolName = options.Value.SchoolName,
            MediaBaseUrl = options.Value.MediaBaseUrl,
            WebPortalUrl = options.Value.WebPortalUrl,
            OnlinePaymentsEnabled = online.GetSettings().Enabled,
        })
    .AllowAnonymous()
    .WithTags("App");

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapLookupEndpoints();
app.MapAcademicEndpoints();
app.MapFamilyEndpoints();
app.MapAttendanceEndpoints();
app.MapResultsEndpoints();
app.MapFinanceEndpoints();
app.MapStoreEndpoints();
app.MapCommunicationEndpoints();
app.MapPortalEndpoints();
app.MapUserEndpoints();

// The database schema, lookup seed data and system roles are owned by the
// web app (it applies EF migrations and seeds on start-up). The mobile API
// never migrates or seeds; it only connects to the same database.
app.Run();

public partial class Program;
