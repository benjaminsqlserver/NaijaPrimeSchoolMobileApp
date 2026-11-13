using NaijaPrimeSchool.Application.Academics;
using NaijaPrimeSchool.Application.Academics.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Read side of the academic structure (sessions, terms, classes, subjects,
// periods, timetable). Setting the structure up stays a desktop job on the
// web app; the mobile app needs it to pick a class / term / lesson.
internal static class AcademicEndpoints
{
    public static void MapAcademicEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api").WithTags("Academics").RequireAuthorization(Policies.Staff);

        staff.MapGet("/sessions", (ISessionService s, CancellationToken ct) => s.ListAsync(ct));
        staff.MapGet("/sessions/current", async (ISessionService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetCurrentAsync(ct)));

        staff.MapGet("/terms", (Guid? sessionId, ITermService s, CancellationToken ct) => s.ListAsync(sessionId, ct));
        staff.MapGet("/terms/current", async (ITermService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetCurrentAsync(ct)));

        staff.MapGet("/classes", (Guid? sessionId, ISchoolClassService s, CancellationToken ct) => s.ListAsync(sessionId, ct));
        staff.MapGet("/subjects", (ISubjectService s, CancellationToken ct) => s.ListAsync(ct));

        var academic = app.MapGroup("/api/timetable").WithTags("Academics").RequireAuthorization(Policies.AcademicStaff);
        academic.MapGet("/periods", (ITimetableService s, CancellationToken ct) => s.ListPeriodsAsync(ct));
        academic.MapGet("", (Guid termId, Guid schoolClassId, ITimetableService s, CancellationToken ct) =>
            s.ListEntriesAsync(new TimetableQuery { TermId = termId, SchoolClassId = schoolClassId }, ct));
        academic.MapGet("/{id:guid}", async (Guid id, ITimetableService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetEntryByIdAsync(id, ct)));
    }
}
