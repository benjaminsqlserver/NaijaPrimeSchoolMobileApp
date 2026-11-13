namespace NaijaPrimeSchool.Mobile.Contracts.Common;

// Public, unauthenticated configuration the API hands every client at
// start-up, so device builds do not hard-code anything that can change.
public class AppConfigDto
{
    public string SchoolName { get; set; } = "Naija Prime School";

    // Base address that relative media paths stored in the database (for
    // example Student.PhotoUrl = "/uploads/students/{id}.jpg") resolve
    // against. Pupil photos are uploaded through, and served by, the web app.
    public string? MediaBaseUrl { get; set; }

    public string? WebPortalUrl { get; set; }

    public bool OnlinePaymentsEnabled { get; set; }
}

// Body of every 4xx the API returns for a business-rule failure. Mirrors
// Application.Common.OperationResult so the UI shows the same messages the
// web app shows.
public class ApiError
{
    public string Title { get; set; } = "Request failed";
    public List<string> Errors { get; set; } = [];
}

public class IdResponse
{
    public Guid Id { get; set; }
}

public class CountResponse
{
    public int Count { get; set; }
}

public class SetActiveRequest
{
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class SetClosedRequest
{
    public bool Closed { get; set; }
}

public class ReplyRequest
{
    public string Body { get; set; } = string.Empty;
}

public class OpenRegisterRequest
{
    public Guid SchoolClassId { get; set; }
    public DateOnly Date { get; set; }
}

public class OpenSubjectRegisterRequest
{
    public Guid TimetableEntryId { get; set; }
    public DateOnly Date { get; set; }
}
