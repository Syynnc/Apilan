using System;

namespace ASI.Basecode.Data.Models
{
    /// <summary>Read-only row of dbo.vw_AttendanceDetails.</summary>
    public partial class AttendanceDetail
    {
        public int AttendanceId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; }
        public string EventType { get; set; }
        public string Venue { get; set; }
        public DateTime EventStart { get; set; }
        public DateTime EventEnd { get; set; }
        public int UserId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
        public string AttendanceMode { get; set; }
        public DateTime TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public int? MinutesAfterStart { get; set; }
        public int IsLate { get; set; }
        public int? MinutesAttended { get; set; }
        public string RecordedBy { get; set; }
    }

    /// <summary>Read-only row of dbo.vw_EventAttendanceSummary.</summary>
    public partial class EventSummary
    {
        public int EventId { get; set; }
        public string EventName { get; set; }
        public string EventType { get; set; }
        public string Venue { get; set; }
        public string EventHost { get; set; }
        public DateTime EventStart { get; set; }
        public DateTime EventEnd { get; set; }
        public int? EventCapacity { get; set; }
        public int? TotalPresent { get; set; }
        public int? TimedOut { get; set; }
        public int? NotYetTimedOut { get; set; }
        public int? LateCount { get; set; }
        public int? SlotsLeft { get; set; }
        public string EventStatus { get; set; }
    }

    /// <summary>Read-only row of dbo.vw_StudentAttendanceSummary (active students only).</summary>
    public partial class StudentSummary
    {
        public int UserId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
        public int? EventsAttended { get; set; }
        public int? TimesLate { get; set; }
        public int? MissingTimeOuts { get; set; }
        public DateTime? LastAttended { get; set; }
    }

    /// <summary>Read-only row of dbo.vw_EventAbsentees (finished events only).</summary>
    public partial class EventAbsentee
    {
        public int EventId { get; set; }
        public string EventName { get; set; }
        public DateTime EventStart { get; set; }
        public int UserId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
    }
}
