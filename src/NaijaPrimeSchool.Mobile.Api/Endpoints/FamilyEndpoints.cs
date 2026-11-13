using NaijaPrimeSchool.Application.Family;
using NaijaPrimeSchool.Application.Family.Dtos;
using NaijaPrimeSchool.Application.Finance;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Students, parents and enrolments (school leaders). Deletes are soft
// deletes: IStudentService / IParentService flag the row (IsDeleted,
// DeletedOn, DeletedBy) and refuse while dependants exist, exactly as on
// the web.
internal static class FamilyEndpoints
{
    public static void MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var leaders = app.MapGroup("/api").WithTags("Family").RequireAuthorization(Policies.SchoolLeaders);

        leaders.MapGet("/students", (QueryFilter<StudentListFilter> query, IStudentService s, CancellationToken ct) =>
        {
            query.Value.Take = Math.Clamp(query.Value.Take, 1, 100);
            return s.ListAsync(query.Value, ct);
        });
        leaders.MapGet("/students/{id:guid}", async (Guid id, IStudentService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        leaders.MapGet("/students/{id:guid}/parents", (Guid id, IStudentService s, CancellationToken ct) =>
            s.GetParentLinksAsync(id, ct));
        leaders.MapPost("/students/{id:guid}/active", async (Guid id, SetActiveRequest body, IStudentService s, CancellationToken ct) =>
            (await s.SetActiveAsync(id, body.IsActive, ct)).ToHttp());
        leaders.MapDelete("/students/{id:guid}", async (Guid id, IStudentService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());

        leaders.MapGet("/parents", (QueryFilter<ParentListFilter> query, IParentService s, CancellationToken ct) =>
        {
            query.Value.Take = Math.Clamp(query.Value.Take, 1, 100);
            return s.ListAsync(query.Value, ct);
        });
        leaders.MapGet("/parents/{id:guid}", async (Guid id, IParentService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        leaders.MapGet("/parents/{id:guid}/students", (Guid id, IParentService s, CancellationToken ct) =>
            s.GetStudentLinksAsync(id, ct));
        leaders.MapPost("/parents/{id:guid}/active", async (Guid id, SetActiveRequest body, IParentService s, CancellationToken ct) =>
            (await s.SetActiveAsync(id, body.IsActive, ct)).ToHttp());
        leaders.MapDelete("/parents/{id:guid}", async (Guid id, IParentService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());

        leaders.MapGet("/enrolments", (QueryFilter<EnrolmentListFilter> query, IEnrolmentService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));

        // Fee ledger for one pupil: the bursar's view (finance roles).
        app.MapGet("/api/students/{id:guid}/ledger", async (Guid id, Guid? termId, IInvoiceService s, CancellationToken ct) =>
                ApiResults.OkOrNotFound(await s.GetStudentLedgerAsync(id, termId, ct)))
            .RequireAuthorization(Policies.Finance)
            .WithTags("Finance");
    }
}
