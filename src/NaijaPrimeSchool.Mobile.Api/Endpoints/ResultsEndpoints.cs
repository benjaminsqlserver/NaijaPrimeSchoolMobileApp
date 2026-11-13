using NaijaPrimeSchool.Application.Results;
using NaijaPrimeSchool.Application.Results.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Gradebook (teachers enter scores on the go), subject results and report
// cards (school leaders review / publish). Report cards for families are
// served by the portal endpoints, which add the ownership checks.
internal static class ResultsEndpoints
{
    public static void MapResultsEndpoints(this IEndpointRouteBuilder app)
    {
        var gradebook = app.MapGroup("/api/assessments").WithTags("Results").RequireAuthorization(Policies.AcademicStaff);

        gradebook.MapGet("", (QueryFilter<TermAssessmentFilter> query, IAssessmentService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        gradebook.MapGet("/{id:guid}", async (Guid id, IAssessmentService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        gradebook.MapGet("/{id:guid}/scores", async (Guid id, IAssessmentService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetScoreSheetAsync(id, ct)));
        gradebook.MapPut("/{id:guid}/scores", async (Guid id, BulkSetScoresRequest body, IAssessmentService s, CancellationToken ct) =>
        {
            body.TermAssessmentId = id;
            foreach (var score in body.Scores) score.TermAssessmentId = id;
            return (await s.BulkSetScoresAsync(body, ct)).ToHttp();
        });
        gradebook.MapPost("/{id:guid}/publish", async (Guid id, IAssessmentService s, CancellationToken ct) =>
            (await s.PublishAsync(id, ct)).ToHttp());
        gradebook.MapPost("/{id:guid}/unpublish", async (Guid id, IAssessmentService s, CancellationToken ct) =>
            (await s.UnpublishAsync(id, ct)).ToHttp());

        var leaders = app.MapGroup("/api").WithTags("Results").RequireAuthorization(Policies.SchoolLeaders);

        leaders.MapGet("/results", (QueryFilter<SubjectResultFilter> query, IResultService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));

        leaders.MapGet("/report-cards", (QueryFilter<ReportCardFilter> query, IReportCardService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        leaders.MapGet("/report-cards/{id:guid}", async (Guid id, IReportCardService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        leaders.MapPut("/report-cards/{id:guid}/comments", async (Guid id, UpdateReportCardCommentsRequest body, IReportCardService s, CancellationToken ct) =>
        {
            body.Id = id;
            return (await s.UpdateCommentsAsync(body, ct)).ToHttp();
        });
        leaders.MapPost("/report-cards/{id:guid}/publish", async (Guid id, IReportCardService s, CancellationToken ct) =>
            (await s.PublishAsync(id, ct)).ToHttp());
        leaders.MapPost("/report-cards/{id:guid}/unpublish", async (Guid id, IReportCardService s, CancellationToken ct) =>
            (await s.UnpublishAsync(id, ct)).ToHttp());
    }
}
