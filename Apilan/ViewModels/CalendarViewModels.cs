using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>The public event directory.</summary>
public class CalendarViewModel
{
    public List<CalendarCard> Events { get; set; } = new();
    public List<EventTypeOption> Types { get; set; } = new();
    public List<OrganizationOption> Organizations { get; set; } = new();

    public int? TypeId { get; set; }
    public int? OrganizationId { get; set; }
    public string? Search { get; set; }

    /// <summary>Registrations the signed-in member already holds, by event id.</summary>
    public Dictionary<int, RegistrationStatus> MyRegistrations { get; set; } = new();

    public bool IsSignedInMember { get; set; }
}

// ===========================================================================
//  MONTH GRID
// ===========================================================================

/// <summary>
/// A real month, server-rendered: the grid of day cells with the events that
/// fall on each, plus one selected day's detail. The JSON feed behind the
/// landing page's calendar is built from the same projection, so the marketing
/// page and the live calendar can never show different months.
/// </summary>
public class CalendarMonthViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }

    public List<CalendarDay> Days { get; set; } = new();

    /// <summary>The day whose events are listed beside the grid.</summary>
    public DateTime Selected { get; set; }

    public List<EventTypeOption> Types { get; set; } = new();
    public List<OrganizationOption> Organizations { get; set; } = new();

    public int? TypeId { get; set; }
    public int? OrganizationId { get; set; }

    public Dictionary<int, RegistrationStatus> MyRegistrations { get; set; } = new();
    public bool IsSignedInMember { get; set; }

    public DateTime FirstOfMonth => new(Year, Month, 1);
    public string MonthName => FirstOfMonth.ToString("MMMM");

    public DateTime PreviousMonth => FirstOfMonth.AddMonths(-1);
    public DateTime NextMonth => FirstOfMonth.AddMonths(1);

    /// <summary>Blank cells before the 1st, so the grid lines up under Sunday.</summary>
    public int LeadingBlanks => (int)FirstOfMonth.DayOfWeek;

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    /// <summary>Blank cells after the last day, to square the final week off.</summary>
    public int TrailingBlanks
    {
        get
        {
            var used = (LeadingBlanks + DaysInMonth) % 7;
            return used == 0 ? 0 : 7 - used;
        }
    }

    public List<CalendarCard> SelectedEvents => Days
        .FirstOrDefault(d => d.Date == Selected.Date)?.Events ?? new List<CalendarCard>();

    public int MonthEventCount => Days.Sum(d => d.Events.Count);
}

/// <summary>One cell in the month grid.</summary>
public class CalendarDay
{
    public DateTime Date { get; set; }
    public List<CalendarCard> Events { get; set; } = new();

    public int Day => Date.Day;
    public bool IsToday => Date.Date == DateTime.Today;
    public bool HasEvents => Events.Any();

    /// <summary>Up to three colour pips, matching the design system's calendar.</summary>
    public IEnumerable<string> Pips => Events.Take(3).Select(e => e.ColorKey);
}

/// <summary>
/// The shape the landing page's calendar script reads. Deliberately terse —
/// it goes over the wire on a marketing page.
/// </summary>
public record CalendarFeedEntry(
    string d,
    string name,
    string type,
    string time,
    string venue,
    string org,
    int taken,
    int cap,
    int id);

public class CalendarCard
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string OrgName { get; set; } = string.Empty;
    public string OrgAcronym { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string? VenueLocation { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public int Taken { get; set; }
    public int Sessions { get; set; }
    public bool AllowWaitlist { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IssuesCertificate { get; set; }
    public EventStatus Status { get; set; }

    public int Remaining => Math.Max(0, Capacity - Taken);
    public bool IsFull => Taken >= Capacity;
    public int FillPercent => Capacity <= 0 ? 0 : Math.Min(100, (int)Math.Round(Taken * 100.0 / Capacity));

    public bool IsOpen => Status is EventStatus.Published or EventStatus.Ongoing
                          && RegistrationDeadline >= DateTime.Now
                          && EndsAt >= DateTime.Now;

    /// <summary>What the button says, given the state of the event.</summary>
    public string CallToAction => !IsOpen ? "Registration closed"
        : IsFull && AllowWaitlist ? "Join the waitlist"
        : IsFull ? "Full"
        : "Register";

    public string TimeRange => StartsAt.Date == EndsAt.Date
        ? $"{StartsAt:h:mm tt} – {EndsAt:h:mm tt}"
        : $"{StartsAt:d MMM h:mm tt} – {EndsAt:d MMM h:mm tt}";
}

/// <summary>A member's own stubs.</summary>
public class MyEventsViewModel
{
    public string FullName { get; set; } = string.Empty;
    public bool HasMemberRecord { get; set; }

    public List<MyEventRow> Upcoming { get; set; } = new();
    public List<MyEventRow> Past { get; set; } = new();

    public int CertificatesEarned => Past.Count(p => p.Earned);

    /// <summary>Events the member could still say something about.</summary>
    public int FeedbackPending => Past.Count(p => p.CanLeaveFeedback && !p.HasFeedback);
}

public class MyEventRow
{
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public TicketViewModel Ticket { get; set; } = new();
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public RegistrationStatus Status { get; set; }

    /// <summary>Sessions turned up for — present or late.</summary>
    public int Attended { get; set; }

    /// <summary>How many of those arrivals were late.</summary>
    public int Late { get; set; }

    public int Sessions { get; set; }
    public EligibilityStatus Eligibility { get; set; }
    public bool Earned { get; set; }

    /// <summary>A member may drop out until the event has started.</summary>
    public bool CanCancel => Status != RegistrationStatus.Cancelled && StartsAt > DateTime.Now;

    // ---- Feedback ---------------------------------------------------------

    public bool CollectsFeedback { get; set; }
    public bool HasFeedback { get; set; }
    public DateTime? FeedbackOn { get; set; }

    /// <summary>
    /// Feedback opens once the event is over, and only for people who actually
    /// turned up — somebody who never came has nothing to report.
    /// </summary>
    public bool CanLeaveFeedback => CollectsFeedback
                                    && Attended > 0
                                    && EndsAt <= DateTime.Now
                                    && Status != RegistrationStatus.Cancelled;
}
