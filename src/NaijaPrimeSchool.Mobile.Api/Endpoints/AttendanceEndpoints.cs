using NaijaPrimeSchool.Application.Attendance;
using NaijaPrimeSchool.Application.Attendance.Dtos;
using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Daily class registers and per-lesson (subject) registers, with the same
// open / mark / submit / reopen lifecycle as the web pages. Attendance
// statuses (Present, Late, Absent, ...) are lookup rows, not enums.
internal static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance").WithTags("Attendance").RequireAuthorization(Policies.AcademicStaff);

        // ---- Daily register ----
        group.MapGet("/daily", async (Guid schoolClassId, DateOnly date, IDailyAttendanceService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetForClassDateAsync(schoolClassId, date, ct)));
        group.MapGet("/daily/{id:guid}", async (Guid id, IDailyAttendanceService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        group.MapPost("/daily", async (OpenRegisterRequest body, IDailyAttendanceService s, ICurrentUser user, CancellationToken ct) =>
            (await s.OpenAsync(new OpenDailyRegisterRequest
            {
                SchoolClassId = body.SchoolClassId,
                Date = body.Date,
                TakenById = user.UserId,
            }, ct)).ToIdHttp());
        group.MapPut("/daily/{id:guid}/entries", async (Guid id, BulkSetDailyAttendanceRequest body, IDailyAttendanceService s, CancellationToken ct) =>
        {
            body.RegisterId = id;
            foreach (var e in body.Entries) e.RegisterId = id;
            return (await s.BulkSetAsync(body, ct)).ToHttp();
        });
        group.MapPost("/daily/{id:guid}/submit", async (Guid id, IDailyAttendanceService s, CancellationToken ct) =>
            (await s.SubmitAsync(id, ct)).ToHttp());
        group.MapPost("/daily/{id:guid}/reopen", async (Guid id, IDailyAttendanceService s, CancellationToken ct) =>
            (await s.ReopenAsync(id, ct)).ToHttp());
        group.MapDelete("/daily/{id:guid}", async (Guid id, IDailyAttendanceService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());

        group.MapGet("/summary", (Guid schoolClassId, Guid? termId, IDailyAttendanceService s, CancellationToken ct) =>
            s.GetClassSummaryAsync(schoolClassId, termId, ct));

        // ---- Subject (lesson) register ----
        group.MapGet("/subject", async (Guid timetableEntryId, DateOnly date, ISubjectAttendanceService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetForEntryDateAsync(timetableEntryId, date, ct)));
        group.MapPost("/subject", async (OpenSubjectRegisterRequest body, ISubjectAttendanceService s, ICurrentUser user, CancellationToken ct) =>
            (await s.OpenAsync(new OpenSubjectSessionRequest
            {
                TimetableEntryId = body.TimetableEntryId,
                Date = body.Date,
                TakenById = user.UserId,
            }, ct)).ToIdHttp());
        group.MapPut("/subject/{id:guid}/entries", async (Guid id, BulkSetSubjectAttendanceRequest body, ISubjectAttendanceService s, CancellationToken ct) =>
        {
            body.SessionId = id;
            foreach (var e in body.Entries) e.SessionId = id;
            return (await s.BulkSetAsync(body, ct)).ToHttp();
        });
        group.MapPost("/subject/{id:guid}/submit", async (Guid id, ISubjectAttendanceService s, CancellationToken ct) =>
            (await s.SubmitAsync(id, ct)).ToHttp());
        group.MapPost("/subject/{id:guid}/reopen", async (Guid id, ISubjectAttendanceService s, CancellationToken ct) =>
            (await s.ReopenAsync(id, ct)).ToHttp());
        group.MapDelete("/subject/{id:guid}", async (Guid id, ISubjectAttendanceService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());
    }
}
