using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>
/// Participation across events — the report an org actually has to file at
/// the end of a term.
/// </summary>
public class ParticipationReportViewModel
{
    public List<EventParticipationRow> Rows { get; set; } = new();
    public List<EventTypeOption> Types { get; set; } = new();
    public List<OrganizationOption> Organizations { get; set; } = new();
    public List<string> Courses { get; set; } = new();

    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? TypeId { get; set; }
    public int? OrganizationId { get; set; }
    public string? Course { get; set; }

    public int Events => Rows.Count;
    public int Registered => Rows.Sum(r => r.Registered);
    public int Attended => Rows.Sum(r => r.Attended);
    public int WalkIns => Rows.Sum(r => r.WalkIns);
    public int Certificates => Rows.Sum(r => r.Certificates);

    public int TurnoutPercent => Registered <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Registered);

    /// <summary>The busiest slice, for the summary line above the table.</summary>
    public EventParticipationRow? Best => Rows
        .Where(r => r.Registered > 0)
        .OrderByDescending(r => r.TurnoutPercent)
        .ThenByDescending(r => r.Attended)
        .FirstOrDefault();
}

public class EventParticipationRow
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string OrgAcronym { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public EventStatus Status { get; set; }
    public int Sessions { get; set; }

    /// <summary>Slots held — confirmed plus pending, never the cancelled ones.</summary>
    public int Registered { get; set; }

    /// <summary>People who turned up for at least one session.</summary>
    public int Attended { get; set; }

    public int WalkIns { get; set; }
    public int Certificates { get; set; }

    /// <summary>Participants who were late to at least one session.</summary>
    public int LateArrivals { get; set; }

    /// <summary>Feedback on this event, when any came in.</summary>
    public int Responses { get; set; }
    public double? AverageRating { get; set; }

    public int NoShows => Math.Max(0, Registered - Attended);
    public int TurnoutPercent => Registered <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Registered);
}

/// <summary>The post-activity report for one event.</summary>
public class EventReportViewModel
{
    public Event Event { get; set; } = null!;

    public List<SessionSummary> Sessions { get; set; } = new();
    public List<ParticipantReportRow> Participants { get; set; } = new();

    public List<BreakdownRow> ByCourse { get; set; } = new();
    public List<BreakdownRow> ByYear { get; set; } = new();

    public int Registered { get; set; }
    public int Attended { get; set; }
    public int WalkIns { get; set; }
    public int Certificates { get; set; }
    public int Waitlisted { get; set; }
    public int Cancelled { get; set; }

    public int NoShows => Math.Max(0, Registered - Attended);
    public int TurnoutPercent => Registered <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Registered);

    /// <summary>Participants who were late to at least one session.</summary>
    public int LateArrivals => Participants.Count(p => p.Late > 0);

    /// <summary>Feedback gathered on this event.</summary>
    public int FeedbackResponses { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackRecommendPercent { get; set; }

    /// <summary>
    /// The responses themselves, for the workbook's feedback sheet. Anonymous
    /// ones are already stripped of the name by the time they land here.
    /// </summary>
    public List<FeedbackResponseRow> Feedback { get; set; } = new();

    /// <summary>Average share of sessions attended, over everyone who turned up.</summary>
    public int AverageAttendancePercent
    {
        get
        {
            var present = Participants.Where(p => p.Attended > 0).ToList();
            return present.Count == 0 ? 0 : (int)Math.Round(present.Average(p => p.AttendancePercent));
        }
    }
}

public class ParticipantReportRow
{
    public int MemberId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public int YearLevel { get; set; }
    public string? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public bool IsWalkIn { get; set; }
    public EligibilityStatus Eligibility { get; set; }
    public bool Earned { get; set; }

    /// <summary>
    /// One cell per session id, so the attendance matrix can be printed with
    /// the same three verdicts the door recorded.
    /// </summary>
    public Dictionary<int, SessionAttendanceCell> SessionCells { get; set; } = new();

    /// <summary>Sessions turned up for — present or late.</summary>
    public int Attended => SessionCells.Count(s => s.Value.Attended);

    public int Late => SessionCells.Count(s => s.Value.Status == AttendanceStatus.Late);
    public int Absent => SessionCells.Count(s => s.Value.Status == AttendanceStatus.Absent);

    public int Sessions { get; set; }
    public int AttendancePercent => Sessions <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Sessions);

    public string CourseYear => $"{Course} · {YearLevel}";

    /// <summary>Their feedback score, if they left any.</summary>
    public double? FeedbackRating { get; set; }
}

/// <summary>
/// What happened to one participant at one session. A null status means no
/// record at all — nobody scanned them and nobody wrote them off either.
/// </summary>
public record SessionAttendanceCell(AttendanceStatus? Status, DateTime? TimeIn, DateTime? TimeOut)
{
    public static readonly SessionAttendanceCell None = new(null, null, null);

    public bool Attended => Status is AttendanceStatus.Present or AttendanceStatus.Late;

    /// <summary>What the cell says on screen and in the spreadsheet.</summary>
    public string Label => Status switch
    {
        AttendanceStatus.Present => TimeIn?.ToString("h:mm tt") ?? "Present",
        AttendanceStatus.Late => $"Late · {TimeIn:h:mm tt}",
        AttendanceStatus.Absent => "Absent",
        _ => "—"
    };

    /// <summary>The one-word version, for a column that has to stay narrow.</summary>
    public string ShortLabel => Status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Late => "Late",
        AttendanceStatus.Absent => "Absent",
        _ => "No record"
    };

    public string Chip => Status switch
    {
        AttendanceStatus.Present => "ok",
        AttendanceStatus.Late => "warn",
        AttendanceStatus.Absent => "risk",
        _ => "idle"
    };
}

public class BreakdownRow
{
    public string Label { get; set; } = string.Empty;
    public int Registered { get; set; }
    public int Attended { get; set; }

    public int Percent => Registered <= 0 ? 0 : (int)Math.Round(Attended * 100.0 / Registered);
}
