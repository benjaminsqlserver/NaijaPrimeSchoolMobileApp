using NaijaPrimeSchool.Domain.Identity;

namespace NaijaPrimeSchool.Mobile.Contracts.Common;

// Role groupings copied 1:1 from the [Authorize(Roles = ...)] attributes on
// the web app's pages, so a user gets exactly the same access on mobile as
// on the web. The API turns each group into an authorization policy and the
// UI uses the same strings to show / hide menu items.
public static class RoleGroups
{
    // Sessions, terms, classes, students, parents, results, report cards,
    // announcements, office inbox.
    public const string SchoolLeaders = $"{Roles.SuperAdmin},{Roles.HeadTeacher}";

    // Timetable, attendance, assessments / score sheets.
    public const string AcademicStaff = $"{Roles.SuperAdmin},{Roles.HeadTeacher},{Roles.Teacher}";

    // Bursar dashboard, invoices, payments, online payments.
    public const string Finance = $"{Roles.SuperAdmin},{Roles.HeadTeacher},{Roles.SchoolBursar}";

    // Store dashboard, catalog, stock movements.
    public const string Store = $"{Roles.SuperAdmin},{Roles.HeadTeacher},{Roles.SchoolStoreKeeper}";

    // Parent / student portal features (reminder settings, paying fees).
    public const string Family = $"{Roles.Parent},{Roles.Student}";

    // Announcement feed and report-card viewer (family + school leaders).
    public const string PortalViewers = $"{Roles.Parent},{Roles.Student},{Roles.SuperAdmin},{Roles.HeadTeacher}";

    // Office inbox (staff side) or own conversations (family side).
    public const string Messaging = $"{Roles.SuperAdmin},{Roles.HeadTeacher},{Roles.Parent},{Roles.Student}";

    // User management.
    public const string UserAdmins = Roles.SuperAdmin;

    // Everyone on staff (shared academic look-ups such as sessions and terms).
    public const string Staff = $"{Roles.SuperAdmin},{Roles.HeadTeacher},{Roles.Teacher},{Roles.SchoolBursar},{Roles.SchoolStoreKeeper}";

    public static string[] Split(string group) =>
        group.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// Authorization policy names registered by the API (one per role group).
public static class Policies
{
    public const string SchoolLeaders = nameof(SchoolLeaders);
    public const string AcademicStaff = nameof(AcademicStaff);
    public const string Finance = nameof(Finance);
    public const string Store = nameof(Store);
    public const string Family = nameof(Family);
    public const string PortalViewers = nameof(PortalViewers);
    public const string Messaging = nameof(Messaging);
    public const string UserAdmins = nameof(UserAdmins);
    public const string Staff = nameof(Staff);

    public static readonly IReadOnlyDictionary<string, string> RoleGroupByPolicy = new Dictionary<string, string>
    {
        [SchoolLeaders] = RoleGroups.SchoolLeaders,
        [AcademicStaff] = RoleGroups.AcademicStaff,
        [Finance] = RoleGroups.Finance,
        [Store] = RoleGroups.Store,
        [Family] = RoleGroups.Family,
        [PortalViewers] = RoleGroups.PortalViewers,
        [Messaging] = RoleGroups.Messaging,
        [UserAdmins] = RoleGroups.UserAdmins,
        [Staff] = RoleGroups.Staff,
    };
}
