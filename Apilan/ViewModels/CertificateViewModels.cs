using Apilan.Models;

namespace Apilan.ViewModels;

public class CertificateEventRow
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string TypeLabel { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public EventStatus Status { get; set; }
    public int ThresholdPercent { get; set; }
    public int Sessions { get; set; }
    public int Participants { get; set; }
    public int Earned { get; set; }

    public int EarnedPercent => Participants <= 0 ? 0 : (int)Math.Round(Earned * 100.0 / Participants);
    public bool IsSettled => Status == EventStatus.Completed;
}

public class CertificateListViewModel
{
    public Event Event { get; set; } = null!;
    public List<CertificateRow> Rows { get; set; } = new();
    public string? Filter { get; set; }

    public int Total { get; set; }
    public int Earned { get; set; }
    public int Overridden { get; set; }

    /// <summary>
    /// Participants whose stored decision no longer matches the rule — the
    /// prompt to recompute after a late attendance correction.
    /// </summary>
    public int Unsettled { get; set; }

    public int EarnedPercent => Total <= 0 ? 0 : (int)Math.Round(Earned * 100.0 / Total);
}

public class CertificateRow
{
    public int RegistrationId { get; set; }
    public int MemberId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string CourseYear { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public int Attended { get; set; }
    public int Sessions { get; set; }
    public EligibilityStatus Eligibility { get; set; }
    public string? Note { get; set; }

    /// <summary>What the attendance rule says, before any override.</summary>
    public bool MeetsRule { get; set; }

    public bool IsOverride => Eligibility is EligibilityStatus.OverrideGranted or EligibilityStatus.OverrideRevoked;
    public bool Earned => Eligibility is EligibilityStatus.Eligible or EligibilityStatus.OverrideGranted;

    public int AttendancePercent => Sessions <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Sessions);

    /// <summary>True when the stored decision disagrees with the rule.</summary>
    public bool NeedsSettling => !IsOverride && MeetsRule != Earned;
}
