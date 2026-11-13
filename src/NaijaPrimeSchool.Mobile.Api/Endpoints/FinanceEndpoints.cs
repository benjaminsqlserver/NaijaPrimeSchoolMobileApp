using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Application.Finance;
using NaijaPrimeSchool.Application.Finance.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Bursar features: dashboard, invoices, receipts (recording a cash / POS /
// transfer payment at the gate is the classic mobile use case), refunds and
// the online-payments review queue.
internal static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Finance").RequireAuthorization(Policies.Finance);

        group.MapGet("/finance/summary", (Guid? termId, IPaymentService s, CancellationToken ct) =>
            s.GetSummaryAsync(termId, ct));

        group.MapGet("/invoices", (QueryFilter<InvoiceFilter> query, IInvoiceService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        group.MapGet("/invoices/{id:guid}", async (Guid id, IInvoiceService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));

        group.MapGet("/payments", (QueryFilter<PaymentFilter> query, IPaymentService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        group.MapGet("/payments/{id:guid}", async (Guid id, IPaymentService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        group.MapPost("/payments", async (RecordPaymentRequest body, IPaymentService s, ICurrentUser user, CancellationToken ct) =>
        {
            body.CollectedById ??= user.UserId;
            return (await s.RecordAsync(body, ct)).ToIdHttp();
        });
        group.MapPost("/payments/{id:guid}/refund", async (Guid id, IPaymentService s, CancellationToken ct) =>
            (await s.RefundAsync(id, ct)).ToHttp());

        group.MapGet("/online-payments", (QueryFilter<OnlinePaymentFilter> query, IOnlinePaymentService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        group.MapPost("/online-payments/{reference}/verify", async (string reference, IOnlinePaymentService s, CancellationToken ct) =>
            (await s.ConfirmAsync(reference, ct)).ToHttp());
    }
}
