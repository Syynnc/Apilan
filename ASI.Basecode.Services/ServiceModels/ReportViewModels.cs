using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.ServiceModels
{
    public class DashboardViewModel
    {
        public int ActiveStudents { get; set; }
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int AttendanceRecords { get; set; }
        public int MissingTimeOuts { get; set; }
        public List<EventSummary> Ongoing { get; set; } = new();
        public List<EventSummary> Upcoming { get; set; } = new();
        public List<EventSummary> RecentlyFinished { get; set; } = new();
        public int UpcomingTotal { get; set; }
        public int FinishedTotal { get; set; }
        public int LateArrivals { get; set; }
    }

    /// <summary>What the participation report is narrowed to. Every field is optional.</summary>
    public class ReportFilter
    {
        // Who
        public string Course { get; set; }
        public int? YearLevel { get; set; }

        // What and when
        public int? EventTypeId { get; set; }
        public int? EventId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Course) && !YearLevel.HasValue && !EventTypeId.HasValue
            && !EventId.HasValue && !From.HasValue && !To.HasValue;
    }

    /// <summary>One event's turnout, split by arrival.</summary>
    public class EventTurnout
    {
        public int EventId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public DateTime Start { get; set; }
        public string Status { get; set; }
        public int? Capacity { get; set; }
        public int OnTime { get; set; }
        public int Late { get; set; }
        public int MissingTimeOut { get; set; }
        public int Total => OnTime + Late;
    }

    /// <summary>A group of members (a course, a year level) and how many of them took part.</summary>
    public class GroupParticipation
    {
        public string Label { get; set; }
        public int Members { get; set; }
        public int Participants { get; set; }
        public int CheckIns { get; set; }
        public double Rate => Members > 0 ? (double)Participants / Members : 0;
    }

    /// <summary>Check-ins for one event type, and the average per event of that type.</summary>
    public class TypeTurnout
    {
        public string Label { get; set; }
        public int Events { get; set; }
        public int CheckIns { get; set; }
        public double Average => Events > 0 ? (double)CheckIns / Events : 0;
    }

    /// <summary>A slice of arrival times relative to the start, split by on time / late.</summary>
    public class ArrivalBucket
    {
        public string Label { get; set; }
        public int OnTime { get; set; }
        public int Late { get; set; }
        public int Total => OnTime + Late;
    }

    public class MemberParticipation
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
        public int Attended { get; set; }
        public int Late { get; set; }
        public int MissingTimeOuts { get; set; }
        public DateTime? LastAttended { get; set; }

        /// <summary>Share of the events in scope this member attended.</summary>
        public double Rate { get; set; }
    }

    public class ReportViewModel
    {
        public ReportFilter Filter { get; set; } = new();

        // Filter choices
        public List<string> Courses { get; set; } = new();
        public List<LookupItem> EventTypes { get; set; } = new();
        public List<LookupItem> Events { get; set; } = new();

        // Headline figures
        public int MembersInScope { get; set; }
        public int Participants { get; set; }
        public int EventsInScope { get; set; }
        public int CheckIns { get; set; }
        public int OnTime { get; set; }
        public int Late { get; set; }
        public int MissingTimeOuts { get; set; }
        public double? AverageMinutesAttended { get; set; }

        public double ParticipationRate => MembersInScope > 0 ? (double)Participants / MembersInScope : 0;
        public double OnTimeRate => CheckIns > 0 ? (double)OnTime / CheckIns : 0;
        public double AveragePerEvent => EventsInScope > 0 ? (double)CheckIns / EventsInScope : 0;

        // Breakdowns
        public List<EventTurnout> PerEvent { get; set; } = new();
        public List<GroupParticipation> ByCourse { get; set; } = new();
        public List<GroupParticipation> ByYear { get; set; } = new();
        public List<TypeTurnout> ByType { get; set; } = new();
        public List<ArrivalBucket> Arrivals { get; set; } = new();
        public List<MemberParticipation> Members { get; set; } = new();
    }
}
