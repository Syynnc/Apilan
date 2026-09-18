using System.ComponentModel.DataAnnotations;
using Apilan.Models;

namespace Apilan.ViewModels;

// ===========================================================================
//  LISTS
// ===========================================================================

public class EventListViewModel
{
    public List<EventRowViewModel> Events { get; set; } = new();

    public EventStatus? Status { get; set; }
    public string? Search { get; set; }

    /// <summary>Filter to one organization's events; null shows every org.</summary>
    public int? OrganizationId { get; set; }
    public List<OrganizationOption> Organizations { get; set; } = new();

    public int DraftCount { get; set; }
    public int PublishedCount { get; set; }
    public int CompletedCount { get; set; }
}

public class EventRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public int OrganizationId { get; set; }
    public string OrgAcronym { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public int Taken { get; set; }
    public int Waitlisted { get; set; }
    public int Sessions { get; set; }
    public EventStatus Status { get; set; }

    public int FillPercent => Capacity <= 0 ? 0 : Math.Min(100, (int)Math.Round(Taken * 100.0 / Capacity));
    public bool IsFull => Taken >= Capacity;

    public string StatusChip => Status switch
    {
        EventStatus.Published => "ok",
        EventStatus.Ongoing => "info",
        EventStatus.Completed => "idle",
        EventStatus.Cancelled => "risk",
        _ => "warn"
    };
}

// ===========================================================================
//  EVENT FORM
// ===========================================================================

/// <summary>
/// Create or edit an event, sessions included. Sessions are edited inline
/// because an event without its attendance blocks is only half an event.
/// </summary>
public class EventFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required, StringLength(140)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Organization")]
    public int OrganizationId { get; set; }

    [Required]
    [Display(Name = "Event type")]
    public int EventTypeId { get; set; }

    [Required]
    [Display(Name = "Venue")]
    public int VenueId { get; set; }

    [Display(Name = "Starts")]
    public DateTime StartsAt { get; set; } = DateTime.Today.AddDays(7).AddHours(13);

    [Display(Name = "Ends")]
    public DateTime EndsAt { get; set; } = DateTime.Today.AddDays(7).AddHours(16);

    [Display(Name = "Registration closes")]
    public DateTime RegistrationDeadline { get; set; } = DateTime.Today.AddDays(6).AddHours(23);

    [Range(1, 100000)]
    public int Capacity { get; set; } = 50;

    [Display(Name = "Registrations need officer approval")]
    public bool RequiresApproval { get; set; }

    [Display(Name = "Keep a waitlist once full")]
    public bool AllowWaitlist { get; set; } = true;

    [Display(Name = "Issue certificates for this event")]
    public bool IssuesCertificate { get; set; } = true;

    [Display(Name = "Ask participants for feedback afterwards")]
    public bool CollectsFeedback { get; set; } = true;

    [Range(0, 100)]
    [Display(Name = "Attendance needed for a certificate (%)")]
    public int CertificateThresholdPercent { get; set; } = 80;

    public List<SessionFormRow> Sessions { get; set; } = new();

    public EventStatus Status { get; set; } = EventStatus.Draft;
    public bool IsNew => Id == 0;

    /// <summary>Filled by the controller for the dropdowns.</summary>
    public List<EventTypeOption> EventTypes { get; set; } = new();
    public List<VenueOption> Venues { get; set; } = new();
    public List<OrganizationOption> Organizations { get; set; } = new();

    /// <summary>Capacity of the chosen venue, for the "over the room" warning.</summary>
    public int SelectedVenueCapacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndsAt <= StartsAt)
        {
            yield return new ValidationResult(
                "The event has to end after it starts.", new[] { nameof(EndsAt) });
        }

        if (RegistrationDeadline > StartsAt)
        {
            yield return new ValidationResult(
                "Registration has to close by the time the event starts.", new[] { nameof(RegistrationDeadline) });
        }

        var live = Sessions.Where(s => !s.IsDeleted && !string.IsNullOrWhiteSpace(s.Name)).ToList();

        if (live.Count == 0)
        {
            yield return new ValidationResult(
                "Add at least one session — attendance is taken per session.", new[] { nameof(Sessions) });
        }

        foreach (var (session, index) in live.Select((s, i) => (s, i)))
        {
            if (session.EndsAt <= session.StartsAt)
            {
                yield return new ValidationResult(
                    $"Session {index + 1} has to end after it starts.",
                    new[] { $"Sessions[{index}].EndsAt" });
            }

            if (session.StartsAt < StartsAt || session.EndsAt > EndsAt)
            {
                yield return new ValidationResult(
                    $"Session {index + 1} falls outside the event's own start and end.",
                    new[] { $"Sessions[{index}].StartsAt" });
            }
        }
    }
}

public class SessionFormRow
{
    public int Id { get; set; }

    [StringLength(80)]
    [Display(Name = "Session name")]
    public string Name { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    [Range(0, 240)]
    [Display(Name = "Late after (min)")]
    public int LateAfterMinutes { get; set; } = 15;

    /// <summary>Set by the remove button; the row stays posted so indexes hold.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Attendance already recorded, so the screen can refuse to drop it.</summary>
    public int AttendanceCount { get; set; }
}

public record EventTypeOption(int Id, string Name, string ColorKey);
public record VenueOption(int Id, string Name, int Capacity, string? Location);

// ===========================================================================
//  EVENT DETAIL
// ===========================================================================

public class EventDetailsViewModel
{
    public Event Event { get; set; } = null!;

    public List<RosterRow> Roster { get; set; } = new();
    public List<SessionSummary> Sessions { get; set; } = new();

    public string? RosterFilter { get; set; }
    public string? Search { get; set; }

    public int Confirmed { get; set; }
    public int Pending { get; set; }
    public int Waitlisted { get; set; }
    public int Cancelled { get; set; }
    public int WalkIns { get; set; }

    /// <summary>Slots held — confirmed plus pending.</summary>
    public int Taken => Confirmed + Pending;
    public int Remaining => Math.Max(0, Event.Capacity - Taken);
    public int FillPercent => Event.Capacity <= 0 ? 0 : Math.Min(100, (int)Math.Round(Taken * 100.0 / Event.Capacity));

    public bool IsOpen { get; set; }
    public bool CanPublish => Event.Status == EventStatus.Draft;
    public bool CanComplete => Event.Status is EventStatus.Published or EventStatus.Ongoing;
    public bool CanCancel => Event.Status is EventStatus.Draft or EventStatus.Published or EventStatus.Ongoing;

    /// <summary>
    /// Attendance on record anywhere in this event. Once someone has been
    /// checked in, the event is a historical fact and can only be cancelled.
    /// </summary>
    public int AttendanceRecords { get; set; }

    /// <summary>
    /// Deleting is for the draft nobody joined and the mistake made five
    /// minutes ago. Anything with attendance behind it gets cancelled instead,
    /// so the reports keep their history.
    /// </summary>
    public bool CanDelete => AttendanceRecords == 0
                             && Event.Status is EventStatus.Draft or EventStatus.Cancelled;

    /// <summary>Why the delete button is not there, said plainly.</summary>
    public string? DeleteBlockedReason => CanDelete ? null
        : AttendanceRecords > 0
            ? $"{AttendanceRecords} attendance record(s) are on file, so this event is part of the org's history."
            : "Only a draft or a cancelled event can be deleted. Cancel it first.";

    /// <summary>Feedback gathered so far, once the event is over.</summary>
    public int FeedbackResponses { get; set; }
    public double? FeedbackAverage { get; set; }

    /// <summary>
    /// This event's own audit trail — who created it, who edited it, and what
    /// they changed. Shown inline because "who moved the venue" is a question
    /// officers ask about their own event, and the full log is admin-only.
    /// </summary>
    public List<AuditRow> History { get; set; } = new();

    /// <summary>Members not yet on the roster, for the walk-in picker.</summary>
    public List<MemberOption> AddableMembers { get; set; } = new();
}

public record MemberOption(int Id, string ListName, string StudentNumber);

public class RosterRow
{
    public int RegistrationId { get; set; }
    public int MemberId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string CourseYear { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public int? WaitlistPosition { get; set; }
    public bool IsWalkIn { get; set; }
    public DateTime RegisteredOn { get; set; }

    /// <summary>Sessions turned up for — present plus late.</summary>
    public int Attended { get; set; }

    /// <summary>How many of those arrivals were late.</summary>
    public int Late { get; set; }

    /// <summary>Sessions an officer explicitly marked them absent for.</summary>
    public int Absent { get; set; }

    public int Sessions { get; set; }
    public EligibilityStatus Eligibility { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Whether they have left feedback, once the event is over.</summary>
    public bool HasFeedback { get; set; }

    public string StatusChip => Status switch
    {
        RegistrationStatus.Confirmed => "ok",
        RegistrationStatus.Pending => "info",
        RegistrationStatus.Waitlisted => "warn",
        _ => "risk"
    };

    public int AttendancePercent => Sessions <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Sessions);
}

public class SessionSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    /// <summary>On time.</summary>
    public int Present { get; set; }

    /// <summary>Turned up past the grace period.</summary>
    public int Late { get; set; }

    /// <summary>Accounted for and did not come.</summary>
    public int Absent { get; set; }

    public int StillIn { get; set; }

    public int LateAfterMinutes { get; set; }

    /// <summary>Everybody who turned up at all — present plus late.</summary>
    public int Attended => Present + Late;

    /// <summary>Rows on file, however they turned out.</summary>
    public int Recorded => Present + Late + Absent;

    public bool IsNow => DateTime.Now >= StartsAt.AddHours(-1) && DateTime.Now <= EndsAt.AddHours(1);
    public string TimeRange => $"{StartsAt:ddd d MMM · h:mm tt} – {EndsAt:h:mm tt}";

    /// <summary>The moment an arrival stops counting as on time.</summary>
    public DateTime LateThreshold => StartsAt.AddMinutes(LateAfterMinutes);
}
