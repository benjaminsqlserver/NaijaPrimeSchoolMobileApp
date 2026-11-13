using NaijaPrimeSchool.Application.Communications;
using NaijaPrimeSchool.Application.Communications.Dtos;
using NaijaPrimeSchool.Application.Messaging;
using NaijaPrimeSchool.Application.Messaging.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Announcements (management + personal feed), two-way messaging and
// reminder preferences. Publishing an announcement queues email / SMS rows
// through the same NotificationService as the web app; the web app's
// background dispatcher sends them.
internal static class CommunicationEndpoints
{
    public static void MapCommunicationEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- Announcement management (school leaders) ----
        var manage = app.MapGroup("/api/announcements").WithTags("Announcements").RequireAuthorization(Policies.SchoolLeaders);

        manage.MapGet("", (QueryFilter<AnnouncementFilter> query, IAnnouncementService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        manage.MapGet("/{id:guid}", async (Guid id, IAnnouncementService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));
        manage.MapPost("", async (CreateAnnouncementRequest body, IAnnouncementService s, CancellationToken ct) =>
            (await s.CreateAsync(body, ct)).ToIdHttp());
        manage.MapPut("/{id:guid}", async (Guid id, UpdateAnnouncementRequest body, IAnnouncementService s, CancellationToken ct) =>
        {
            body.Id = id;
            return (await s.UpdateAsync(body, ct)).ToHttp();
        });
        manage.MapPost("/{id:guid}/publish", async (Guid id, IAnnouncementService s, CancellationToken ct) =>
            (await s.PublishAsync(id, ct)).ToHttp());
        manage.MapPost("/{id:guid}/unpublish", async (Guid id, IAnnouncementService s, CancellationToken ct) =>
            (await s.UnpublishAsync(id, ct)).ToHttp());
        manage.MapDelete("/{id:guid}", async (Guid id, IAnnouncementService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());

        // ---- Personal announcement feed ----
        var feed = app.MapGroup("/api/my/announcements").WithTags("Announcements").RequireAuthorization(Policies.PortalViewers);
        feed.MapGet("", (int? take, IAnnouncementService s, CancellationToken ct) =>
            s.ListForCurrentUserAsync(Math.Clamp(take ?? 30, 1, 100), ct));
        feed.MapGet("/unread-count", async (IAnnouncementService s, CancellationToken ct) =>
            new CountResponse { Count = await s.CountUnreadForCurrentUserAsync(ct) });
        feed.MapPost("/{id:guid}/read", async (Guid id, IAnnouncementService s, CancellationToken ct) =>
            (await s.MarkAsReadAsync(id, ct)).ToHttp());

        // ---- Reminder preferences (parents / students) ----
        var prefs = app.MapGroup("/api/my/notification-preferences").WithTags("Announcements").RequireAuthorization(Policies.Family);
        prefs.MapGet("", async (INotificationPreferenceService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetForCurrentUserAsync(ct)));
        prefs.MapPut("", async (UpdateNotificationPreferenceRequest body, INotificationPreferenceService s, CancellationToken ct) =>
            (await s.SaveForCurrentUserAsync(body, ct)).ToHttp());

        // ---- Messaging (office inbox or own conversations; the service
        //      decides which side the caller is on) ----
        var messages = app.MapGroup("/api/messages").WithTags("Messaging").RequireAuthorization(Policies.Messaging);
        messages.MapGet("", (QueryFilter<InboxFilter> query, IMessagingService s, CancellationToken ct) =>
            s.ListThreadsAsync(query.Value, ct));
        messages.MapGet("/unread-count", async (IMessagingService s, CancellationToken ct) =>
            new CountResponse { Count = await s.CountUnreadThreadsAsync(ct) });
        messages.MapGet("/{id:guid}", async (Guid id, IMessagingService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetThreadAsync(id, ct)));
        messages.MapPost("", async (StartThreadRequest body, IMessagingService s, CancellationToken ct) =>
            (await s.StartThreadAsync(body, ct)).ToIdHttp());
        messages.MapPost("/{id:guid}/reply", async (Guid id, ReplyRequest body, IMessagingService s, CancellationToken ct) =>
            (await s.ReplyAsync(id, body.Body, ct)).ToHttp());
        messages.MapGet("/student-options", (Guid? familyUserId, IMessagingService s, CancellationToken ct) =>
            s.GetStudentOptionsAsync(familyUserId, ct));

        // Staff-only operations.
        messages.MapPost("/{id:guid}/closed", async (Guid id, SetClosedRequest body, IMessagingService s, CancellationToken ct) =>
                (await s.SetClosedAsync(id, body.Closed, ct)).ToHttp())
            .RequireAuthorization(Policies.SchoolLeaders);
        messages.MapGet("/recipients", (IMessagingService s, CancellationToken ct) => s.GetRecipientsAsync(ct))
            .RequireAuthorization(Policies.SchoolLeaders);
    }
}
