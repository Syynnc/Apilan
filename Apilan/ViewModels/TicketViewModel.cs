using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>
/// What a stub shows as the registration's state. This is a display concern,
/// not a domain one: "Checked in" is derived from attendance records rather
/// than stored on <see cref="Registration"/>.
/// </summary>
public enum TicketStatus
{
    Pending,
    Confirmed,
    Waitlisted,
    CheckedIn,
    Cancelled
}

/// <summary>
/// One registration stub. Rendered by Views/Shared/_Ticket.cshtml — the same
/// component backs the landing page, the registration confirmation screen,
/// and My Events.
/// </summary>
public class TicketViewModel
{
    public string Org { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Colour key from the event type master record: talk, workshop, assembly,
    /// service, or training. Drives the datepad band and the calendar pip.
    /// </summary>
    public string TypeKey { get; set; } = "talk";

    /// <summary>Human label for the event type, e.g. "Seminar".</summary>
    public string TypeLabel { get; set; } = "Event";

    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public string Venue { get; set; } = string.Empty;
    public string Session { get; set; } = "Single session";

    public string RegistrantName { get; set; } = string.Empty;
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>Short human-typeable check-in code, e.g. APL-7K2M.</summary>
    public string Code { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Confirmed;

    /// <summary>Tilt toward the cursor. Hero only — off in lists.</summary>
    public bool Interactive { get; set; }

    public string StatusChip => Status switch
    {
        TicketStatus.Confirmed => "info",
        TicketStatus.CheckedIn => "ok",
        TicketStatus.Waitlisted => "warn",
        TicketStatus.Pending => "idle",
        TicketStatus.Cancelled => "risk",
        _ => "idle"
    };

    public string StatusLabel => Status switch
    {
        TicketStatus.Confirmed => "Confirmed",
        TicketStatus.CheckedIn => "Checked in",
        TicketStatus.Waitlisted => "Waitlisted",
        TicketStatus.Pending => "Pending",
        TicketStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    /// <summary>The note printed on the tear-off stub.</summary>
    public string StubNote => Status switch
    {
        TicketStatus.Waitlisted => "Code issues if a slot opens",
        TicketStatus.Cancelled => "This stub is void",
        TicketStatus.CheckedIn => "Scan again to time out",
        TicketStatus.Pending => "Awaiting officer approval",
        _ => "Scan at the door"
    };

    public string TimeRange => EndsAt.HasValue
        ? $"{StartsAt:h:mm tt} – {EndsAt.Value:h:mm tt}"
        : StartsAt.ToString("h:mm tt");

    /// <summary>Builds a stub from the real records once data is wired.</summary>
    public static TicketViewModel FromRegistration(
        Registration registration, string orgName, bool checkedIn = false)
    {
        var ev = registration.Event;

        return new TicketViewModel
        {
            Org = orgName,
            Title = ev?.Title ?? string.Empty,
            TypeKey = ev?.EventType?.ColorKey ?? "talk",
            TypeLabel = ev?.EventType?.Name ?? "Event",
            StartsAt = ev?.StartsAt ?? DateTime.Now,
            EndsAt = ev?.EndsAt,
            Venue = ev?.Venue?.Name ?? string.Empty,
            Session = ev is null || ev.Sessions.Count <= 1
                ? "Single session"
                : $"{ev.Sessions.Count} sessions",
            RegistrantName = registration.Member?.FullName ?? string.Empty,
            StudentNo = registration.Member?.StudentNumber ?? string.Empty,
            Code = registration.Code,
            Status = checkedIn
                ? TicketStatus.CheckedIn
                : registration.Status switch
                {
                    RegistrationStatus.Confirmed => TicketStatus.Confirmed,
                    RegistrationStatus.Waitlisted => TicketStatus.Waitlisted,
                    RegistrationStatus.Cancelled => TicketStatus.Cancelled,
                    _ => TicketStatus.Pending
                }
        };
    }
}
