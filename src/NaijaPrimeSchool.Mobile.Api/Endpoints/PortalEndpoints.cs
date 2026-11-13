using System.Net;
using Microsoft.Extensions.Options;
using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Application.Family;
using NaijaPrimeSchool.Application.Finance;
using NaijaPrimeSchool.Application.Finance.Dtos;
using NaijaPrimeSchool.Application.Portals;
using NaijaPrimeSchool.Application.Results;
using NaijaPrimeSchool.Application.Results.Dtos;
using NaijaPrimeSchool.Domain.Identity;
using NaijaPrimeSchool.Mobile.Contracts.Common;
using NaijaPrimeSchool.Mobile.Contracts.Portal;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Parent and student portal. The ownership rules are the web portal pages'
// rules, applied server-side: a parent only ever sees linked wards, a
// student only themselves, and only *published* report cards.
internal static class PortalEndpoints
{
    public static void MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").WithTags("Portal");

        group.MapGet("/parent", async (IPortalService portal, CancellationToken ct) =>
            {
                var parentId = await portal.ResolveParentIdForCurrentUserAsync(ct);
                return parentId is null
                    ? Results.NotFound()
                    : ApiResults.OkOrNotFound(await portal.GetParentDashboardAsync(parentId.Value, ct));
            })
            .RequireAuthorization(p => p.RequireRole(Roles.Parent));

        group.MapGet("/student", async (IPortalService portal, CancellationToken ct) =>
            {
                var studentId = await portal.ResolveStudentIdForCurrentUserAsync(ct);
                return studentId is null
                    ? Results.NotFound()
                    : ApiResults.OkOrNotFound(await portal.GetStudentDashboardAsync(studentId.Value, ct));
            })
            .RequireAuthorization(p => p.RequireRole(Roles.Student));

        group.MapGet("/student/report-cards", async (IPortalService portal, IReportCardService cards, CancellationToken ct) =>
            {
                var studentId = await portal.ResolveStudentIdForCurrentUserAsync(ct);
                if (studentId is null) return Results.Ok(Array.Empty<ReportCardDto>());
                return Results.Ok(await cards.ListAsync(new ReportCardFilter { StudentId = studentId, IsPublished = true }, ct));
            })
            .RequireAuthorization(p => p.RequireRole(Roles.Student));

        group.MapGet("/student/fees", async (IPortalService portal, IInvoiceService invoices, IOnlinePaymentService online, CancellationToken ct) =>
            {
                var studentId = await portal.ResolveStudentIdForCurrentUserAsync(ct);
                return Results.Ok(new StudentFeesDto
                {
                    Ledger = studentId is null ? null : await invoices.GetStudentLedgerAsync(studentId.Value, null, ct),
                    CanPayOnline = online.GetSettings().Enabled,
                });
            })
            .RequireAuthorization(p => p.RequireRole(Roles.Student));

        // Ward / pupil detail: parent (linked ward), student (self) or school leader.
        group.MapGet("/students/{studentId:guid}", async (
                Guid studentId, IPortalService portal, IStudentService students,
                IReportCardService cards, IInvoiceService invoices, IOnlinePaymentService online,
                ICurrentUser user, CancellationToken ct) =>
            {
                if (!await portal.CurrentUserCanViewStudentAsync(studentId, ct)) return Results.Forbid();
                var student = await students.GetByIdAsync(studentId, ct);
                if (student is null) return Results.NotFound();

                return Results.Ok(new WardDetailDto
                {
                    Student = student,
                    Dashboard = await portal.GetStudentDashboardAsync(studentId, ct),
                    ReportCards = (await cards.ListAsync(new ReportCardFilter { StudentId = studentId, IsPublished = true }, ct)).ToList(),
                    Ledger = await invoices.GetStudentLedgerAsync(studentId, null, ct),
                    // Same rule as the web: only the parent pays from the ward page.
                    CanPayOnline = online.GetSettings().Enabled && user.IsInRole(Roles.Parent),
                });
            })
            .RequireAuthorization(Policies.PortalViewers);

        group.MapGet("/report-cards/{id:guid}", async (
                Guid id, IReportCardService cards, IPortalService portal, ICurrentUser user, CancellationToken ct) =>
            {
                var found = await cards.GetByIdAsync(id, ct);
                if (found is null) return Results.NotFound();

                var isStaff = user.IsInRole(Roles.SuperAdmin) || user.IsInRole(Roles.HeadTeacher);
                if (isStaff) return Results.Ok(found);
                if (!found.Card.IsPublished) return Results.NotFound();

                if (user.IsInRole(Roles.Student))
                {
                    var me = await portal.ResolveStudentIdForCurrentUserAsync(ct);
                    if (me == found.Card.StudentId) return Results.Ok(found);
                }
                if (user.IsInRole(Roles.Parent) && await portal.CurrentUserCanViewStudentAsync(found.Card.StudentId, ct))
                    return Results.Ok(found);

                return Results.NotFound();
            })
            .RequireAuthorization(Policies.PortalViewers);

        // ---- Online fee payment (Paystack) ----
        group.MapGet("/invoices/{id:guid}", async (
                Guid id, IInvoiceService invoices, IPortalService portal, IOnlinePaymentService online, CancellationToken ct) =>
            {
                var detail = await invoices.GetByIdAsync(id, ct);
                if (detail is null || !await portal.CurrentUserCanViewStudentAsync(detail.Invoice.StudentId, ct))
                    return Results.NotFound();
                var settings = online.GetSettings();
                return Results.Ok(new PortalInvoiceDto
                {
                    Detail = detail,
                    Settings = settings,
                    MinimumAmount = Math.Min(settings.MinimumAmount, detail.Invoice.Balance),
                });
            })
            .RequireAuthorization(Policies.Family);

        group.MapPost("/payments", async (
                StartMobilePaymentRequest body, IOnlinePaymentService online, HttpRequest request,
                IOptions<MobileApiOptions> options, CancellationToken ct) =>
            {
                // The gateway sends the payer's browser back here; the page
                // below re-verifies with the gateway and tells them to return
                // to the app (which then confirms again for its own display).
                var returnUrl = $"{PublicBaseUrl(request, options.Value)}/api/portal/payments/return";
                var result = await online.StartAsync(new StartOnlinePaymentRequest
                {
                    InvoiceId = body.InvoiceId,
                    Amount = body.Amount,
                    ReturnUrl = returnUrl,
                }, ct);
                return result.ToHttp();
            })
            .RequireAuthorization(Policies.Family);

        group.MapPost("/payments/{reference}/confirm", async (string reference, IOnlinePaymentService online, CancellationToken ct) =>
            {
                // Ownership first (null when not theirs), then re-verify with
                // the gateway: never trust the redirect itself.
                if (await online.GetByReferenceAsync(reference, ct) is null) return Results.NotFound();
                await online.ConfirmAsync(reference, ct);
                return ApiResults.OkOrNotFound(await online.GetByReferenceAsync(reference, ct));
            })
            .RequireAuthorization(Policies.Family);

        group.MapGet("/payments/return", async (string? reference, string? trxref, IOnlinePaymentService online, CancellationToken ct) =>
            {
                var r = reference ?? trxref;
                string message;
                if (string.IsNullOrWhiteSpace(r))
                {
                    message = "We could not find a payment reference.";
                }
                else
                {
                    var confirmed = await online.ConfirmAsync(r, ct);
                    message = confirmed is { Succeeded: true, Data: { } p }
                        ? $"Payment {WebUtility.HtmlEncode(p.Reference)}: {WebUtility.HtmlEncode(p.StatusName)}."
                        : "We are still confirming your payment.";
                }

                var html = $$"""
                    <!doctype html><html lang="en"><head><meta charset="utf-8">
                    <meta name="viewport" content="width=device-width,initial-scale=1">
                    <title>Payment received</title>
                    <style>body{font-family:system-ui,Segoe UI,Arial,sans-serif;background:#f8faf9;color:#1a1f36;display:flex;min-height:100vh;align-items:center;justify-content:center;margin:0}
                    .card{background:#fff;border-radius:14px;padding:2rem;max-width:420px;margin:1rem;box-shadow:0 6px 24px rgba(15,23,42,.08);text-align:center}
                    .badge{display:inline-block;background:#f5a524;color:#064e2b;font-weight:800;border-radius:10px;padding:.5rem .75rem;margin-bottom:1rem}</style>
                    </head><body><div class="card"><div class="badge">NPS</div>
                    <h1 style="font-size:1.3rem">Thank you</h1><p>{{message}}</p>
                    <p>You can close this window and return to the Naija Prime School app, then tap <strong>Check payment status</strong>.</p>
                    </div></body></html>
                    """;
                return Results.Content(html, "text/html");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static string PublicBaseUrl(HttpRequest request, MobileApiOptions options) =>
        !string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? options.PublicBaseUrl.TrimEnd('/')
            : $"{request.Scheme}://{request.Host}{request.PathBase}";
}
