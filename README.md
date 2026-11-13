# Naija Prime School — Mobile

The mobile companion to the **NaijaPrimeSchool** web app, built with **.NET 10**, **.NET MAUI Blazor Hybrid**, **Blazor (Interactive Auto)**, **Clean Architecture**, **EF Core + SQL Server** and **Radzen Blazor Components**.

It runs on **Android, iOS, macOS (Mac Catalyst) and Windows** as a native MAUI app. **Linux** (and any other device with a browser) uses the same UI through a Blazor Web App host in **Interactive Auto** render mode, because .NET MAUI has no Linux target.

It uses **the same SQL Server database** and **the same ASP.NET Core Identity users, passwords, roles and lockout rules** as the web app.

---

## Architecture

```
            ┌────────────────────────────── NaijaPrimeSchoolWeb/src (existing) ──────────────────────────────┐
            │  Domain  ◄──  Application  ◄──  Infrastructure (EF Core, SQL Server, Identity, services, audit)  │
            └──────────────────────▲───────────────────────────────────────────▲──────────────────────────────┘
                                   │ DTOs / role names                         │ same services, same DbContext
 NaijaPrimeSchoolMobileApp/src     │                                           │
   Mobile.Contracts ───────────────┘                               Mobile.Api (ASP.NET Core, JWT) ──► SQL Server
        ▲                                                                ▲        (NaijaPrimeSchool DB, shared)
        │                                                                │ HTTPS + JWT
   Mobile.Shared (Razor UI, Radzen, API clients, auth state) ────────────┘
        ▲                         ▲
   Mobile (MAUI Blazor Hybrid)   Mobile.Web + Mobile.Web.Client (Blazor Web App, Interactive Auto)
   Android · iOS · macOS · Win   Linux · any browser
```

| Project | Layer | What it does |
|---|---|---|
| `NaijaPrimeSchool.Domain` / `.Application` / `.Infrastructure` *(web repo)* | Domain / Application / Infrastructure | Referenced, **not copied**: entities, `BaseEntity` (audit + soft-delete columns), lookup-table entities, EF model & migrations, Identity, every business service, audit trail. One source of truth for both apps. |
| `NaijaPrimeSchool.Mobile.Contracts` | Application (wire contracts) | Auth DTOs, role groups / policy names, composite portal DTOs. Re-uses the Application DTOs (`StudentDto`, `InvoiceDto`, `LookupDto`, …) unchanged. |
| `NaijaPrimeSchool.Mobile.Api` | Presentation (backend-for-mobile) | Minimal-API endpoints over the Application services, JWT bearer auth against the shared Identity store, role policies mirroring the web pages. |
| `NaijaPrimeSchool.Mobile.Shared` | Presentation (UI) + client infrastructure | All pages/components (Radzen), typed API clients, session/token handling, `AuthenticationStateProvider`. |
| `NaijaPrimeSchool.Mobile` | Host | .NET MAUI Blazor Hybrid app (Android, iOS, Mac Catalyst, Windows). Secure token storage via `SecureStorage`. |
| `NaijaPrimeSchool.Mobile.Web` / `.Web.Client` | Host | Blazor Web App, `InteractiveAuto` (server first, then WebAssembly). For Linux and browsers. |

### Why an API instead of connecting the phone straight to SQL Server?

The requirement is that the mobile app stores and fetches data from the same database and authenticates against the same users — it does, through the API. A database connection string inside an app package can be extracted by anyone who installs it (every parent and student), which would expose the whole school database; SQL Server's client is also not supported on iOS/Android. Routing through `Mobile.Api` keeps the credentials on the server **and** means every write goes through the web app's own services, so validation, soft delete, lookups, audit trail, notification queueing and portal-account rules behave identically.

## Architectural decisions carried over from the web app

| Decision | How the mobile app follows it |
|---|---|
| **Clean Architecture** | Mobile API depends on Application/Infrastructure; UI depends only on Contracts (DTOs); hosts only add platform services. |
| **Soft delete** | Every delete calls the existing `SoftDeleteAsync` service methods (`IsDeleted`, `DeletedOn`, `DeletedBy` set by `ApplicationDbContext`); query filters hide deleted rows. Guards are unchanged (e.g. a parent with linked wards can't be deleted, a submitted register can't be deleted, deleting a stock movement reverses its effect). Deleted users can't sign in or refresh tokens. |
| **Lookup data in tables, not enums** | Attendance statuses, payment methods, invoice statuses, movement types, announcement categories/audiences, etc. are fetched from `/api/lookups/{name}` (backed by `ILookupService`) and used by Id/Code. Badge colours and form rules key on the same lookup **codes** the web app uses. |
| **Audit trail** | Changes made from mobile go through the same `ApplicationDbContext.SaveChanges`, so they land in the audit log attributed to the signed-in user (`ICurrentUser` is fed from the JWT). Refresh tokens live in Identity's `UserTokens` table, which the audit trail already excludes as secret. |
| **Role-based access** | `RoleGroups`/`Policies` copy the `[Authorize(Roles = …)]` of each web page; the API enforces them, the UI hides what a role can't use. Portal ownership checks (parent → linked wards only, student → self, published report cards only) are enforced server-side. |
| **Identity rules** | Same password hasher, password policy, lockout (5 attempts / 15 min), "deactivated" and soft-deleted checks as the web Login page. Tokens are revalidated on every request (1-minute cache): deactivating a user, changing their password or roles (security stamp) cuts off mobile access too. |

No database migrations are added: the mobile API never migrates or seeds — the web app owns the schema.

## Features by role

| Role | Mobile features |
|---|---|
| **Parent** | My wards (class, attendance %, outstanding fees, report-card count), ward detail (today's lessons, attendance, profile, report cards, fee ledger), report card viewer, **pay fees online** (Paystack, re-verified server-side), announcements (mark read), messages with the office, reminder settings (email/SMS/quiet hours). |
| **Student** | Today (lessons + attendance), my results/report cards, my fees + online payment, my profile, announcements, messages, reminder settings. |
| **Teacher** | Timetable, **daily register** and **lesson register** (open, mark with one tap per pupil, save, submit, reopen), attendance summary (same ≥90% / ≥75% colour bands), **score sheets** (enter/save scores, absent flag, publish/unpublish). |
| **Head teacher / Super admin** | Everything teachers have, plus students & parents (view, call/SMS/email, activate/deactivate, soft delete), report cards (review, publish one or all), announcements (create, edit, publish/unpublish, soft delete), office inbox (reply, resolve/reopen), finance and store. |
| **Bursar** | Bursar dashboard, invoices, payments/receipts, **record a payment** with auto-allocation to the oldest invoices, refunds, online-payment review/verify. |
| **Store keeper** | Store dashboard, catalog with low-stock filter, item history, **record a stock movement** (supplier for inbound, pupil/class/staff for outbound), reverse a movement. |
| **Super admin** | User directory, activate/deactivate accounts. |
| **Everyone** | Sign in with web credentials, change password (signs out other devices), choose school server (devices). |

Desktop-heavy set-up stays on the web portal: sessions/terms/classes/subjects/timetable editing, creating students/parents/users and editing profiles/photos, enrolments, fee schedules and invoice issuing, assessment set-up, result computation and report-card generation, audit log, notification log.

## Running locally

Prerequisites: .NET 10 SDK with the `maui`/`android`/`ios`/`maccatalyst` workloads, the web app's SQL Server database (run the web app once so it migrates and seeds), and the two repos side by side:

```
NaijaPrimeSchoolProject/
  NaijaPrimeSchoolWeb/        ← existing web app
  NaijaPrimeSchoolMobileApp/  ← this folder
```
(If they are not siblings, set `NaijaPrimeSchoolWebSrc` in `Directory.Build.props`.)

1. **API** — uses the same connection string as the web app (`src/NaijaPrimeSchool.Mobile.Api/appsettings.json`).
   ```bash
   cd src/NaijaPrimeSchool.Mobile.Api
   dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>"   # dev has a placeholder key
   dotnet run --launch-profile https      # https://localhost:7280 and http://0.0.0.0:5280
   ```
   Set `MobileApi:MediaBaseUrl` to the web app's address so pupil photos load (default `https://localhost:7141`).

2. **Native app (MAUI)** — API address is in `src/NaijaPrimeSchool.Mobile/appsettings.json` (default `http://localhost:5280`; the Android emulator is pointed at `10.0.2.2` automatically). On a real phone, enter the server address under *School server* on the sign-in screen.
   ```bash
   cd src/NaijaPrimeSchool.Mobile
   dotnet build -t:Run -f net10.0-windows10.0.19041.0     # Windows
   dotnet build -t:Run -f net10.0-android                 # Android emulator/device
   dotnet build -t:Run -f net10.0-ios                     # iOS (needs a Mac / paired Mac)
   dotnet build -t:Run -f net10.0-maccatalyst             # macOS (on a Mac)
   ```

3. **Web / Linux host (Blazor Auto)**
   ```bash
   cd src/NaijaPrimeSchool.Mobile.Web
   dotnet run --launch-profile https      # https://localhost:7021
   ```
   API address: `Api:BaseUrl` in `NaijaPrimeSchool.Mobile.Web/appsettings.json` and `NaijaPrimeSchool.Mobile.Web.Client/wwwroot/appsettings.json`. Its origin must be listed in the API's `MobileApi:AllowedOrigins` (CORS).

Sign in with any existing web account (SuperAdmin, HeadTeacher, Teacher, SchoolBursar, SchoolStoreKeeper, Parent or Student).

## Deployment notes

- Host `Mobile.Api` over **HTTPS** next to (or behind the same gateway as) the web app; set `ConnectionStrings:DefaultConnection`, `Jwt:SigningKey` (secret store / `Jwt__SigningKey`), `MobileApi:PublicBaseUrl`, `MobileApi:MediaBaseUrl`, `MobileApi:AllowedOrigins`, and the same `OnlinePayments` / `Notifications` sections as the web app.
- `MobileApi:RunNotificationDispatcher` is `false`: the web app's background worker sends the email/SMS rows that mobile actions queue. Turn it on only if the API runs without the web app.
- Online payments: the API supports the **Paystack** provider. The web app's in-memory *Simulator* can't be used from the API (its checkout page lives in the web app).
- The plain-HTTP allowances (Android `network_security_config.xml`, iOS `NSAllowsLocalNetworking`) only cover the local development machine.
- The browser host keeps the session in `localStorage`; the native app uses the OS keychain/keystore.
