using Microsoft.AspNetCore.Authorization;
using NaijaPrimeSchool.Application.Users;
using NaijaPrimeSchool.Application.Users.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Lookup data lives in database tables (Genders, AttendanceStatuses,
// PaymentMethods, StockMovementTypes, ...) rather than C# enums, exactly as
// in the web app. Clients always fetch the rows from here and work with
// their Ids / Codes; nothing is hard-coded on the device.
internal static class LookupEndpoints
{
    private sealed record Lookup(string? Policy, Func<ILookupService, string?, Guid?, CancellationToken, Task<IReadOnlyList<LookupDto>>> Load);

    // null policy = any signed-in user (static reference data).
    private static readonly Dictionary<string, Lookup> Lookups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["genders"] = new(null, (s, _, _, ct) => s.GetGendersAsync(ct)),
        ["titles"] = new(null, (s, _, _, ct) => s.GetTitlesAsync(ct)),
        ["term-types"] = new(null, (s, _, _, ct) => s.GetTermTypesAsync(ct)),
        ["class-levels"] = new(null, (s, _, _, ct) => s.GetClassLevelsAsync(ct)),
        ["week-days"] = new(null, (s, _, _, ct) => s.GetWeekDaysAsync(ct)),
        ["relationships"] = new(null, (s, _, _, ct) => s.GetRelationshipsAsync(ct)),
        ["enrolment-statuses"] = new(null, (s, _, _, ct) => s.GetEnrolmentStatusesAsync(ct)),
        ["blood-groups"] = new(null, (s, _, _, ct) => s.GetBloodGroupsAsync(ct)),
        ["marital-statuses"] = new(null, (s, _, _, ct) => s.GetMaritalStatusesAsync(ct)),
        ["attendance-statuses"] = new(null, (s, _, _, ct) => s.GetAttendanceStatusesAsync(ct)),
        ["assessment-types"] = new(null, (s, _, _, ct) => s.GetAssessmentTypesAsync(ct)),
        ["grade-bands"] = new(null, (s, _, _, ct) => s.GetGradeBandsAsync(ct)),
        ["affective-traits"] = new(null, (s, _, _, ct) => s.GetAffectiveTraitsAsync(ct)),
        ["psychomotor-skills"] = new(null, (s, _, _, ct) => s.GetPsychomotorSkillsAsync(ct)),
        ["trait-ratings"] = new(null, (s, _, _, ct) => s.GetTraitRatingsAsync(ct)),
        ["fee-categories"] = new(null, (s, _, _, ct) => s.GetFeeCategoriesAsync(ct)),
        ["payment-methods"] = new(null, (s, _, _, ct) => s.GetPaymentMethodsAsync(ct)),
        ["invoice-statuses"] = new(null, (s, _, _, ct) => s.GetInvoiceStatusesAsync(ct)),
        ["payment-statuses"] = new(null, (s, _, _, ct) => s.GetPaymentStatusesAsync(ct)),
        ["item-categories"] = new(null, (s, _, _, ct) => s.GetItemCategoriesAsync(ct)),
        ["units-of-measure"] = new(null, (s, _, _, ct) => s.GetUnitsOfMeasureAsync(ct)),
        ["stock-movement-types"] = new(null, (s, _, _, ct) => s.GetStockMovementTypesAsync(ct)),
        ["announcement-categories"] = new(null, (s, _, _, ct) => s.GetAnnouncementCategoriesAsync(ct)),
        ["announcement-audiences"] = new(null, (s, _, _, ct) => s.GetAnnouncementAudiencesAsync(ct)),
        ["notification-channels"] = new(null, (s, _, _, ct) => s.GetNotificationChannelsAsync(ct)),
        ["notification-statuses"] = new(null, (s, _, _, ct) => s.GetNotificationStatusesAsync(ct)),

        // People and school structure: staff only.
        ["classes"] = new(Policies.Staff, (s, _, sessionId, ct) => s.GetClassesForSessionAsync(sessionId, ct)),
        ["teachers"] = new(Policies.Staff, (s, _, _, ct) => s.GetTeachersAsync(ct)),
        ["students"] = new(Policies.Staff, (s, q, _, ct) => s.GetStudentsAsync(q, ct)),
        ["parents"] = new(Policies.SchoolLeaders, (s, q, _, ct) => s.GetParentsAsync(q, ct)),
        ["suppliers"] = new(Policies.Store, (s, _, _, ct) => s.GetActiveSuppliersAsync(ct)),
        ["store-items"] = new(Policies.Store, (s, q, _, ct) => s.GetStoreItemsAsync(q, ct)),
    };

    public static void MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lookups/{name}", async (
                string name, string? search, Guid? sessionId,
                HttpContext http, ILookupService lookups,
                IAuthorizationService authz, CancellationToken ct) =>
            {
                if (!Lookups.TryGetValue(name, out var lookup)) return Results.NotFound();

                if (lookup.Policy is not null)
                {
                    var allowed = await authz.AuthorizeAsync(http.User, lookup.Policy);
                    if (!allowed.Succeeded) return Results.Forbid();
                }

                return Results.Ok(await lookup.Load(lookups, search, sessionId, ct));
            })
            .RequireAuthorization()
            .WithTags("Lookups");

        app.MapGet("/api/lookups/roles", (ILookupService lookups, CancellationToken ct) => lookups.GetRolesAsync(ct))
            .RequireAuthorization(Policies.UserAdmins)
            .WithTags("Lookups");
    }
}
