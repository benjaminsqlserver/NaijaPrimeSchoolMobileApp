using NaijaPrimeSchool.Application.Academics.Dtos;
using NaijaPrimeSchool.Application.Attendance.Dtos;
using NaijaPrimeSchool.Application.Communications.Dtos;
using NaijaPrimeSchool.Application.Family.Dtos;
using NaijaPrimeSchool.Application.Finance.Dtos;
using NaijaPrimeSchool.Application.Inventory.Dtos;
using NaijaPrimeSchool.Application.Messaging.Dtos;
using NaijaPrimeSchool.Application.Portals.Dtos;
using NaijaPrimeSchool.Application.Results.Dtos;
using NaijaPrimeSchool.Application.Users.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Auth;
using NaijaPrimeSchool.Mobile.Contracts.Common;
using NaijaPrimeSchool.Mobile.Contracts.Portal;

namespace NaijaPrimeSchool.Mobile.Shared.Services.Api;

// Typed clients, one per feature area. They only describe the HTTP shape;
// every rule lives in the API (i.e. in the web app's Application services).

// Lookup tables (no enums anywhere): cached per signed-in session.
public sealed class LookupClient(ApiClient api)
{
    public const string AttendanceStatuses = "attendance-statuses";
    public const string PaymentMethods = "payment-methods";
    public const string InvoiceStatuses = "invoice-statuses";
    public const string ItemCategories = "item-categories";
    public const string StockMovementTypes = "stock-movement-types";
    public const string AnnouncementCategories = "announcement-categories";
    public const string AnnouncementAudiences = "announcement-audiences";
    public const string AssessmentTypes = "assessment-types";
    public const string Classes = "classes";
    public const string Students = "students";
    public const string Suppliers = "suppliers";
    public const string StoreItems = "store-items";
    public const string Teachers = "teachers";

    private readonly Dictionary<string, List<LookupDto>> cache = new();

    public async Task<List<LookupDto>> GetAsync(string name)
    {
        if (cache.TryGetValue(name, out var hit)) return hit;
        var rows = await api.GetListAsync<LookupDto>($"api/lookups/{name}");
        cache[name] = rows;
        return rows;
    }

    // Uncached searches (students, store items) and session-scoped classes.
    public Task<List<LookupDto>> SearchAsync(string name, string? search = null, Guid? sessionId = null) =>
        api.GetListAsync<LookupDto>(Query.Build($"api/lookups/{name}", ("search", search), ("sessionId", sessionId)));

    public void Clear() => cache.Clear();
}

public sealed class AcademicClient(ApiClient api)
{
    public Task<List<SessionDto>> SessionsAsync() => api.GetListAsync<SessionDto>("api/sessions");
    public Task<SessionDto?> CurrentSessionAsync() => api.GetAsync<SessionDto>("api/sessions/current");
    public Task<List<TermDto>> TermsAsync(Guid? sessionId = null) =>
        api.GetListAsync<TermDto>(Query.Build("api/terms", ("sessionId", sessionId)));
    public Task<TermDto?> CurrentTermAsync() => api.GetAsync<TermDto>("api/terms/current");
    public Task<List<SchoolClassDto>> ClassesAsync(Guid? sessionId = null) =>
        api.GetListAsync<SchoolClassDto>(Query.Build("api/classes", ("sessionId", sessionId)));
    public Task<List<SubjectDto>> SubjectsAsync() => api.GetListAsync<SubjectDto>("api/subjects");
    public Task<List<TimetablePeriodDto>> PeriodsAsync() => api.GetListAsync<TimetablePeriodDto>("api/timetable/periods");
    public Task<List<TimetableEntryDto>> TimetableAsync(Guid termId, Guid classId) =>
        api.GetListAsync<TimetableEntryDto>(Query.Build("api/timetable", ("termId", termId), ("schoolClassId", classId)));
}

public sealed class AttendanceClient(ApiClient api)
{
    public Task<DailyAttendanceRegisterDetailDto?> DailyAsync(Guid classId, DateOnly date) =>
        api.GetAsync<DailyAttendanceRegisterDetailDto>(Query.Build("api/attendance/daily", ("schoolClassId", classId), ("date", date)));
    public Task<ApiResult<IdResponse>> OpenDailyAsync(Guid classId, DateOnly date) =>
        api.PostAsync<IdResponse>("api/attendance/daily", new OpenRegisterRequest { SchoolClassId = classId, Date = date });
    public Task<ApiResult> SaveDailyAsync(Guid registerId, BulkSetDailyAttendanceRequest body) =>
        api.PutAsync($"api/attendance/daily/{registerId}/entries", body);
    public Task<ApiResult> SubmitDailyAsync(Guid id) => api.PostAsync($"api/attendance/daily/{id}/submit");
    public Task<ApiResult> ReopenDailyAsync(Guid id) => api.PostAsync($"api/attendance/daily/{id}/reopen");
    public Task<ApiResult> DeleteDailyAsync(Guid id) => api.DeleteAsync($"api/attendance/daily/{id}");

    public Task<SubjectAttendanceSessionDetailDto?> SubjectAsync(Guid timetableEntryId, DateOnly date) =>
        api.GetAsync<SubjectAttendanceSessionDetailDto>(Query.Build("api/attendance/subject", ("timetableEntryId", timetableEntryId), ("date", date)));
    public Task<ApiResult<IdResponse>> OpenSubjectAsync(Guid timetableEntryId, DateOnly date) =>
        api.PostAsync<IdResponse>("api/attendance/subject", new OpenSubjectRegisterRequest { TimetableEntryId = timetableEntryId, Date = date });
    public Task<ApiResult> SaveSubjectAsync(Guid sessionId, BulkSetSubjectAttendanceRequest body) =>
        api.PutAsync($"api/attendance/subject/{sessionId}/entries", body);
    public Task<ApiResult> SubmitSubjectAsync(Guid id) => api.PostAsync($"api/attendance/subject/{id}/submit");
    public Task<ApiResult> ReopenSubjectAsync(Guid id) => api.PostAsync($"api/attendance/subject/{id}/reopen");

    public Task<ClassAttendanceSummaryDto?> SummaryAsync(Guid classId, Guid? termId) =>
        api.GetAsync<ClassAttendanceSummaryDto>(Query.Build("api/attendance/summary", ("schoolClassId", classId), ("termId", termId)));
}

public sealed class ResultsClient(ApiClient api)
{
    public Task<List<TermAssessmentDto>> AssessmentsAsync(Guid? termId, Guid? classId, Guid? subjectId) =>
        api.GetListAsync<TermAssessmentDto>(Query.Build("api/assessments", ("termId", termId), ("schoolClassId", classId), ("subjectId", subjectId)));
    public Task<AssessmentScoreSheetDto?> ScoreSheetAsync(Guid id) => api.GetAsync<AssessmentScoreSheetDto>($"api/assessments/{id}/scores");
    public Task<ApiResult> SaveScoresAsync(Guid id, BulkSetScoresRequest body) => api.PutAsync($"api/assessments/{id}/scores", body);
    public Task<ApiResult> PublishAssessmentAsync(Guid id, bool publish) =>
        api.PostAsync($"api/assessments/{id}/{(publish ? "publish" : "unpublish")}");

    public Task<List<ReportCardDto>> ReportCardsAsync(Guid? termId, Guid? classId) =>
        api.GetListAsync<ReportCardDto>(Query.Build("api/report-cards", ("termId", termId), ("schoolClassId", classId)));
    public Task<ApiResult> PublishReportCardAsync(Guid id, bool publish) =>
        api.PostAsync($"api/report-cards/{id}/{(publish ? "publish" : "unpublish")}");
    public Task<ApiResult> SaveCommentsAsync(UpdateReportCardCommentsRequest body) =>
        api.PutAsync($"api/report-cards/{body.Id}/comments", body);
    public Task<List<SubjectResultDto>> SubjectResultsAsync(Guid? termId, Guid? classId, Guid? studentId = null) =>
        api.GetListAsync<SubjectResultDto>(Query.Build("api/results", ("termId", termId), ("schoolClassId", classId), ("studentId", studentId)));
}

public sealed class FamilyClient(ApiClient api)
{
    public async Task<PagedResult<StudentDto>> StudentsAsync(string? search, Guid? classId, bool? isActive, int skip, int take) =>
        await api.GetAsync<PagedResult<StudentDto>>(Query.Build("api/students",
            ("searchTerm", search), ("classId", classId), ("isActive", isActive), ("skip", skip), ("take", take))) ?? new();
    public Task<StudentDto?> StudentAsync(Guid id) => api.GetAsync<StudentDto>($"api/students/{id}");
    public Task<List<StudentParentDto>> StudentParentsAsync(Guid id) => api.GetListAsync<StudentParentDto>($"api/students/{id}/parents");
    public Task<ApiResult> SetStudentActiveAsync(Guid id, bool active) =>
        api.PostAsync($"api/students/{id}/active", new SetActiveRequest { IsActive = active });
    public Task<ApiResult> DeleteStudentAsync(Guid id) => api.DeleteAsync($"api/students/{id}");

    public async Task<PagedResult<ParentDto>> ParentsAsync(string? search, bool? isActive, int skip, int take) =>
        await api.GetAsync<PagedResult<ParentDto>>(Query.Build("api/parents",
            ("searchTerm", search), ("isActive", isActive), ("skip", skip), ("take", take))) ?? new();
    public Task<ParentDto?> ParentAsync(Guid id) => api.GetAsync<ParentDto>($"api/parents/{id}");
    public Task<List<StudentParentDto>> ParentStudentsAsync(Guid id) => api.GetListAsync<StudentParentDto>($"api/parents/{id}/students");
    public Task<ApiResult> SetParentActiveAsync(Guid id, bool active) =>
        api.PostAsync($"api/parents/{id}/active", new SetActiveRequest { IsActive = active });
    public Task<ApiResult> DeleteParentAsync(Guid id) => api.DeleteAsync($"api/parents/{id}");
}

public sealed class FinanceClient(ApiClient api)
{
    public async Task<FinanceSummaryDto> SummaryAsync(Guid? termId) =>
        await api.GetAsync<FinanceSummaryDto>(Query.Build("api/finance/summary", ("termId", termId))) ?? new();
    public Task<List<InvoiceDto>> InvoicesAsync(string? search, Guid? termId, Guid? statusId) =>
        api.GetListAsync<InvoiceDto>(Query.Build("api/invoices", ("search", search), ("termId", termId), ("invoiceStatusId", statusId)));
    public Task<InvoiceDetailDto?> InvoiceAsync(Guid id) => api.GetAsync<InvoiceDetailDto>($"api/invoices/{id}");
    public Task<List<PaymentDto>> PaymentsAsync(string? search, DateOnly? from, DateOnly? to) =>
        api.GetListAsync<PaymentDto>(Query.Build("api/payments", ("search", search), ("fromDate", from), ("toDate", to)));
    public Task<PaymentDetailDto?> PaymentAsync(Guid id) => api.GetAsync<PaymentDetailDto>($"api/payments/{id}");
    public Task<ApiResult<IdResponse>> RecordPaymentAsync(RecordPaymentRequest body) => api.PostAsync<IdResponse>("api/payments", body);
    public Task<ApiResult> RefundAsync(Guid id) => api.PostAsync($"api/payments/{id}/refund");
    public Task<StudentLedgerDto?> LedgerAsync(Guid studentId) => api.GetAsync<StudentLedgerDto>($"api/students/{studentId}/ledger");
    public Task<List<OnlinePaymentDto>> OnlinePaymentsAsync(string? search, string? status) =>
        api.GetListAsync<OnlinePaymentDto>(Query.Build("api/online-payments", ("search", search), ("statusCode", status)));
    public Task<ApiResult> VerifyOnlinePaymentAsync(string reference) =>
        api.PostAsync($"api/online-payments/{Uri.EscapeDataString(reference)}/verify");
}

public sealed class StoreClient(ApiClient api)
{
    public async Task<StoreSummaryDto> SummaryAsync() => await api.GetAsync<StoreSummaryDto>("api/store/summary") ?? new();
    public Task<List<StoreItemDto>> ItemsAsync(string? search, Guid? categoryId, bool? belowReorder) =>
        api.GetListAsync<StoreItemDto>(Query.Build("api/store/items", ("search", search), ("itemCategoryId", categoryId), ("onlyBelowReorder", belowReorder)));
    public Task<StoreItemDetailDto?> ItemAsync(Guid id) => api.GetAsync<StoreItemDetailDto>($"api/store/items/{id}");
    public Task<List<StockMovementDto>> MovementsAsync(string? search, Guid? typeId, DateOnly? from, DateOnly? to) =>
        api.GetListAsync<StockMovementDto>(Query.Build("api/store/movements", ("search", search), ("stockMovementTypeId", typeId), ("fromDate", from), ("toDate", to)));
    public Task<ApiResult<IdResponse>> RecordMovementAsync(RecordStockMovementRequest body) => api.PostAsync<IdResponse>("api/store/movements", body);
    public Task<ApiResult> DeleteMovementAsync(Guid id) => api.DeleteAsync($"api/store/movements/{id}");
}

public sealed class CommsClient(ApiClient api)
{
    // Personal feed
    public Task<List<AnnouncementDto>> MyAnnouncementsAsync() => api.GetListAsync<AnnouncementDto>("api/my/announcements?take=50");
    public async Task<int> UnreadAnnouncementsAsync() => (await api.GetAsync<CountResponse>("api/my/announcements/unread-count"))?.Count ?? 0;
    public Task<ApiResult> MarkReadAsync(Guid id) => api.PostAsync($"api/my/announcements/{id}/read");

    // Management
    public Task<List<AnnouncementDto>> AnnouncementsAsync(string? search, bool? published) =>
        api.GetListAsync<AnnouncementDto>(Query.Build("api/announcements", ("search", search), ("isPublished", published)));
    public Task<AnnouncementDto?> AnnouncementAsync(Guid id) => api.GetAsync<AnnouncementDto>($"api/announcements/{id}");
    public Task<ApiResult<IdResponse>> CreateAnnouncementAsync(CreateAnnouncementRequest body) => api.PostAsync<IdResponse>("api/announcements", body);
    public Task<ApiResult> UpdateAnnouncementAsync(UpdateAnnouncementRequest body) => api.PutAsync($"api/announcements/{body.Id}", body);
    public Task<ApiResult> PublishAnnouncementAsync(Guid id, bool publish) =>
        api.PostAsync($"api/announcements/{id}/{(publish ? "publish" : "unpublish")}");
    public Task<ApiResult> DeleteAnnouncementAsync(Guid id) => api.DeleteAsync($"api/announcements/{id}");

    // Messaging
    public Task<List<MessageThreadSummaryDto>> ThreadsAsync(string? search, bool? open, bool unreadOnly) =>
        api.GetListAsync<MessageThreadSummaryDto>(Query.Build("api/messages", ("search", search), ("open", open), ("unreadOnly", unreadOnly)));
    public Task<MessageThreadDto?> ThreadAsync(Guid id) => api.GetAsync<MessageThreadDto>($"api/messages/{id}");
    public async Task<int> UnreadThreadsAsync() => (await api.GetAsync<CountResponse>("api/messages/unread-count"))?.Count ?? 0;
    public Task<ApiResult<IdResponse>> StartThreadAsync(StartThreadRequest body) => api.PostAsync<IdResponse>("api/messages", body);
    public Task<ApiResult> ReplyAsync(Guid id, string body) => api.PostAsync($"api/messages/{id}/reply", new ReplyRequest { Body = body });
    public Task<ApiResult> SetClosedAsync(Guid id, bool closed) => api.PostAsync($"api/messages/{id}/closed", new SetClosedRequest { Closed = closed });
    public Task<List<MessageRecipientDto>> RecipientsAsync() => api.GetListAsync<MessageRecipientDto>("api/messages/recipients");
    public Task<List<MessageStudentOptionDto>> StudentOptionsAsync(Guid? familyUserId) =>
        api.GetListAsync<MessageStudentOptionDto>(Query.Build("api/messages/student-options", ("familyUserId", familyUserId)));

    // Reminder preferences
    public Task<NotificationPreferenceDto?> PreferencesAsync() => api.GetAsync<NotificationPreferenceDto>("api/my/notification-preferences");
    public Task<ApiResult> SavePreferencesAsync(UpdateNotificationPreferenceRequest body) => api.PutAsync("api/my/notification-preferences", body);
}

public sealed class PortalClient(ApiClient api)
{
    public Task<ParentDashboardDto?> ParentAsync() => api.GetAsync<ParentDashboardDto>("api/portal/parent");
    public Task<StudentDashboardDto?> StudentAsync() => api.GetAsync<StudentDashboardDto>("api/portal/student");
    public Task<List<ReportCardDto>> MyReportCardsAsync() => api.GetListAsync<ReportCardDto>("api/portal/student/report-cards");
    public Task<StudentFeesDto?> MyFeesAsync() => api.GetAsync<StudentFeesDto>("api/portal/student/fees");
    public Task<WardDetailDto?> WardAsync(Guid studentId) => api.GetAsync<WardDetailDto>($"api/portal/students/{studentId}");
    public Task<ReportCardDetailDto?> ReportCardAsync(Guid id) => api.GetAsync<ReportCardDetailDto>($"api/portal/report-cards/{id}");
    public Task<PortalInvoiceDto?> InvoiceAsync(Guid id) => api.GetAsync<PortalInvoiceDto>($"api/portal/invoices/{id}");
    public Task<ApiResult<StartOnlinePaymentResult>> StartPaymentAsync(Guid invoiceId, decimal amount) =>
        api.PostAsync<StartOnlinePaymentResult>("api/portal/payments", new StartMobilePaymentRequest { InvoiceId = invoiceId, Amount = amount });
    public Task<ApiResult<OnlinePaymentDto>> ConfirmPaymentAsync(string reference) =>
        api.PostAsync<OnlinePaymentDto>($"api/portal/payments/{Uri.EscapeDataString(reference)}/confirm", null);
}

public sealed class UserClient(ApiClient api)
{
    public async Task<PagedResult<UserDto>> UsersAsync(string? search, string? role, bool? isActive, int skip, int take) =>
        await api.GetAsync<PagedResult<UserDto>>(Query.Build("api/users",
            ("searchTerm", search), ("role", role), ("isActive", isActive), ("skip", skip), ("take", take))) ?? new();
    public Task<UserDto?> UserAsync(Guid id) => api.GetAsync<UserDto>($"api/users/{id}");
    public Task<ApiResult> SetActiveAsync(Guid id, bool active, string? reason) =>
        api.PostAsync($"api/users/{id}/active", new SetActiveRequest { IsActive = active, Reason = reason });
}

public sealed class AccountClient(ApiClient api)
{
    public Task<ApiResult<AuthTokenResponse>> ChangePasswordAsync(ChangePasswordRequest body) =>
        api.PostAsync<AuthTokenResponse>("api/auth/change-password", body);
}
