using NaijaPrimeSchool.Application.Family.Dtos;
using NaijaPrimeSchool.Application.Finance.Dtos;
using NaijaPrimeSchool.Application.Portals.Dtos;
using NaijaPrimeSchool.Application.Results.Dtos;

namespace NaijaPrimeSchool.Mobile.Contracts.Portal;

// One round-trip for the ward / pupil detail screen. The web WardDetail page
// calls four services in-process; over a mobile network we fetch them in a
// single request.
public class WardDetailDto
{
    public StudentDto Student { get; set; } = new();
    public StudentDashboardDto? Dashboard { get; set; }
    public List<ReportCardDto> ReportCards { get; set; } = [];
    public StudentLedgerDto? Ledger { get; set; }
    public bool CanPayOnline { get; set; }
}

public class StudentFeesDto
{
    public StudentLedgerDto? Ledger { get; set; }
    public bool CanPayOnline { get; set; }
}

public class PortalInvoiceDto
{
    public InvoiceDetailDto Detail { get; set; } = new();
    public OnlinePaymentSettingsDto Settings { get; set; } = new();
    public decimal MinimumAmount { get; set; }
}

public class StartMobilePaymentRequest
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
}
