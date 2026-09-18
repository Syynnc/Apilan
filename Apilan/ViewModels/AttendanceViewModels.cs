using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>The event picker that stands in front of the door console.</summary>
public class AttendancePickerViewModel
{
    public List<AttendanceEventOption> Today { get; set; } = new();
    public List<AttendanceEventOption> Upcoming { get; set; } = new();
    public List<AttendanceEventOption> Recent { get; set; } = new();
}

public class AttendanceEventOption
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public EventStatus Status { get; set; }
    public int Sessions { get; set; }
    public int Expected { get; set; }
    public int Present { get; set; }
}

/// <summary>What the officer at the door works from.</summary>
public class DoorViewModel
{
    public Event Event { get; set; } = null!;
    public List<SessionSummary> Sessions { get; set; } = new();
    public SessionSummary Session { get; set; } = null!;

    public List<DoorRow> Feed { get; set; } = new();

    /// <summary>People expected who have not been recorded yet, for manual search.</summary>
    public List<DoorRow> Expected { get; set; } = new();

    public string? Search { get; set; }

    public int ExpectedCount { get; set; }
    public int PresentCount { get; set; }

    /// <summary>Turned up, but past the session's grace period.</summary>
    public int LateCount { get; set; }

    /// <summary>Explicitly marked absent by an officer.</summary>
    public int AbsentCount { get; set; }

    public int StillInCount { get; set; }
    public int WalkInCount { get; set; }

    /// <summary>People with no record at all yet — neither in nor accounted for.</summary>
    public int UnaccountedCount { get; set; }

    /// <summary>Everybody who turned up at all.</summary>
    public int AttendedCount => PresentCount + LateCount;

    public int PresentPercent => ExpectedCount <= 0
        ? 0
        : Math.Min(100, (int)Math.Round(AttendedCount * 100.0 / ExpectedCount));

    /// <summary>Outcome of the last scan, carried over the redirect.</summary>
    public ScanFeedback? Feedback { get; set; }
}

public class DoorRow
{
    public int RegistrationId { get; set; }
    public int? AttendanceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string CourseYear { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public bool IsWalkIn { get; set; }
    public DateTime? TimeIn { get; set; }
    public DateTime? TimeOut { get; set; }
    public CheckInMethod? Method { get; set; }

    /// <summary>Null when there is no record for this session yet.</summary>
    public AttendanceStatus? AttendanceStatus { get; set; }

    public string? Note { get; set; }

    /// <summary>Turned up — present or late. An absence is a record, not a presence.</summary>
    public bool IsPresent => AttendanceStatus is Models.AttendanceStatus.Present
                                             or Models.AttendanceStatus.Late;

    public bool IsAbsent => AttendanceStatus == Models.AttendanceStatus.Absent;
    public bool IsLate => AttendanceStatus == Models.AttendanceStatus.Late;

    /// <summary>No row at all: not checked in, not accounted for either.</summary>
    public bool IsUnaccounted => AttendanceStatus is null;

    public bool IsStillIn => IsPresent && !TimeOut.HasValue;

    public string StatusChip => AttendanceStatus switch
    {
        Models.AttendanceStatus.Present => "ok",
        Models.AttendanceStatus.Late => "warn",
        Models.AttendanceStatus.Absent => "risk",
        _ => "idle"
    };

    public string StatusLabel => AttendanceStatus switch
    {
        Models.AttendanceStatus.Present => "Present",
        Models.AttendanceStatus.Late => "Late",
        Models.AttendanceStatus.Absent => "Absent",
        _ => "No record"
    };
}

/// <summary>The banner shown after a scan — the officer's only feedback at speed.</summary>
public class ScanFeedback
{
    public string Tone { get; set; } = "ok";
    public string Headline { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Code { get; set; }

    public static ScanFeedback Ok(string headline, string? detail = null, string? code = null) =>
        new() { Tone = "ok", Headline = headline, Detail = detail, Code = code };

    public static ScanFeedback Warn(string headline, string? detail = null, string? code = null) =>
        new() { Tone = "warn", Headline = headline, Detail = detail, Code = code };

    public static ScanFeedback Bad(string headline, string? detail = null, string? code = null) =>
        new() { Tone = "bad", Headline = headline, Detail = detail, Code = code };
}
