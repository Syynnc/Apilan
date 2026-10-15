using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public class ReportService : ServiceBase, IReportService
    {
        private readonly IEventRepository _events;
        private readonly IEventTypeRepository _eventTypes;
        private readonly IUserRepository _users;
        private readonly IAttendanceRepository _attendance;

        public ReportService(IEventRepository events, IEventTypeRepository eventTypes, IUserRepository users,
                             IAttendanceRepository attendance)
        {
            _events = events;
            _eventTypes = eventTypes;
            _users = users;
            _attendance = attendance;
        }

        public string ExportCsv(ReportFilter filter, string generatedBy, out string fileName)
        {
            var r = GetReport(filter);
            var f = r.Filter;
            var inv = CultureInfo.InvariantCulture;
            var csv = new StringBuilder();

            void Row(params object[] cells) =>
                csv.AppendLine(string.Join(",", cells.Select(c => Cell(c switch
                {
                    null => string.Empty,
                    double d => d.ToString("0.##", inv),
                    DateTime t => t.ToString("yyyy-MM-dd HH:mm", inv),
                    _ => Convert.ToString(c, inv),
                }))));
            void Section(string title, params string[] headers)
            {
                csv.AppendLine();
                Row(title);
                if (headers.Length > 0) { Row(headers); }
            }
            string Pct(double v) => Math.Round(v * 100).ToString("0", inv) + "%";

            Row("Apilan participation report");
            Row("Generated", DateTime.Now, "by", generatedBy);

            Section("Filters", "Filter", "Value");
            Row("Course", string.IsNullOrWhiteSpace(f.Course) ? "All courses" : f.Course);
            Row("Year", YearLevels.Label(f.YearLevel) ?? "All years");
            Row("Event type", r.EventTypes.FirstOrDefault(t => t.Id == f.EventTypeId)?.Name ?? "All types");
            Row("Event", r.Events.FirstOrDefault(e => e.Id == f.EventId)?.Name ?? "All events");
            Row("From", f.From?.ToString("yyyy-MM-dd", inv) ?? "Any");
            Row("To", f.To?.ToString("yyyy-MM-dd", inv) ?? "Any");

            Section("Summary", "Measure", "Value", "Note");
            Row("Participation rate", Pct(r.ParticipationRate), $"{r.Participants} of {r.MembersInScope} active members came to at least one event");
            Row("Events in scope", r.EventsInScope, "events that have started");
            Row("Check-ins", r.CheckIns);
            Row("Average check-ins per event", r.AveragePerEvent);
            Row("On time", Pct(r.OnTimeRate), $"{r.OnTime} arrivals inside the time-in window");
            Row("Late", r.Late, "arrivals after the time-in window");
            Row("Missing time-outs", r.MissingTimeOuts);
            Row("Average stay (minutes)", r.AverageMinutesAttended.HasValue ? Math.Round(r.AverageMinutesAttended.Value) : null);

            Section("Attendance per event", "Event", "Type", "Start", "Status", "Capacity", "On time", "Late", "Total", "No time-out");
            foreach (var e in r.PerEvent.OrderBy(e => e.Start))
            {
                Row(e.Name, e.Type, e.Start, e.Status, e.Capacity, e.OnTime, e.Late, e.Total, e.MissingTimeOut);
            }

            Section("By event type", "Type", "Events", "Check-ins", "Average per event");
            foreach (var t in r.ByType) { Row(t.Label, t.Events, t.CheckIns, t.Average); }

            Section("Participation by course", "Course", "Members", "Took part", "Rate", "Check-ins");
            foreach (var g in r.ByCourse) { Row(g.Label, g.Members, g.Participants, Pct(g.Rate), g.CheckIns); }

            Section("Participation by year", "Year", "Members", "Took part", "Rate", "Check-ins");
            foreach (var g in r.ByYear) { Row(g.Label, g.Members, g.Participants, Pct(g.Rate), g.CheckIns); }

            Section("Arrival times (minutes after the start)", "Arrived", "On time", "Late", "Total");
            foreach (var a in r.Arrivals) { Row(a.Label, a.OnTime, a.Late, a.Total); }

            Section("Members", "Name", "Student no.", "Course", "Year", "Events attended", "Of events", "Rate", "Late", "No time-out", "Last attended");
            foreach (var m in r.Members)
            {
                Row(m.FullName, m.StudentNumber, m.Course, YearLevels.Label(m.YearLevel), m.Attended, r.EventsInScope,
                    Pct(m.Rate), m.Late, m.MissingTimeOuts, m.LastAttended?.ToString("yyyy-MM-dd", inv) ?? "Never");
            }

            fileName = $"participation-{DateTime.Now:yyyy-MM-dd-HHmm}.csv";
            return csv.ToString();
        }

        /// <summary>A CSV cell: quoted when it has to be, and never read as a formula.</summary>
        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) { return string.Empty; }
            if ("=+-@".IndexOf(value[0]) >= 0 && !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                value = "'" + value;
            }
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        public DashboardViewModel GetDashboard()
        {
            var events = _events.GetEventSummaries().ToList();

            return new DashboardViewModel
            {
                ActiveStudents = _users.GetUsers().Count(u => u.IsActive),
                TotalEvents = events.Count,
                UpcomingEvents = events.Count(e => e.EventStatus == Const.StatusUpcoming),
                AttendanceRecords = events.Sum(e => e.TotalPresent ?? 0),
                MissingTimeOuts = events.Where(e => e.EventStatus == Const.StatusFinished).Sum(e => e.NotYetTimedOut ?? 0),
                Ongoing = events.Where(e => e.EventStatus == Const.StatusOngoing)
                                .OrderBy(e => e.EventStart).ToList(),
                Upcoming = events.Where(e => e.EventStatus == Const.StatusUpcoming)
                                 .OrderBy(e => e.EventStart).Take(4).ToList(),
                UpcomingTotal = events.Count(e => e.EventStatus == Const.StatusUpcoming),
                // The dashboard shows the last three; the rest are a click away in Events.
                RecentlyFinished = events.Where(e => e.EventStatus == Const.StatusFinished)
                                         .OrderByDescending(e => e.EventEnd).Take(3).ToList(),
                FinishedTotal = events.Count(e => e.EventStatus == Const.StatusFinished),
                LateArrivals = events.Sum(e => e.LateCount ?? 0),
            };
        }

        /// <summary>
        /// The participation report. Members are narrowed by course and year,
        /// events by type, a single event, and a date range on their start.
        /// Only events that have started count — an upcoming event has had no
        /// chance to be attended, so it would only drag every rate down.
        /// </summary>
        public ReportViewModel GetReport(ReportFilter filter)
        {
            filter ??= new ReportFilter();
            var course = string.IsNullOrWhiteSpace(filter.Course) ? null : filter.Course.Trim();

            /* ---- Events in scope ---------------------------------------- */
            var eventQuery = _events.GetEventSummaries().Where(e => e.EventStatus != Const.StatusUpcoming);
            if (filter.EventId.HasValue)
            {
                eventQuery = eventQuery.Where(e => e.EventId == filter.EventId);
            }
            if (filter.EventTypeId.HasValue)
            {
                var typeName = _eventTypes.GetEventTypes().Where(t => t.Id == filter.EventTypeId).Select(t => t.TypeName).FirstOrDefault();
                eventQuery = eventQuery.Where(e => e.EventType == typeName);
            }
            if (filter.From.HasValue)
            {
                var from = filter.From.Value.Date;
                eventQuery = eventQuery.Where(e => e.EventStart >= from);
            }
            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1);
                eventQuery = eventQuery.Where(e => e.EventStart < to);
            }
            var events = eventQuery.OrderBy(e => e.EventStart).ToList();
            var eventIds = events.Select(e => e.EventId).ToList();

            /* ---- Members in scope (active only) ------------------------- */
            var memberQuery = _users.GetUsers().Where(u => u.IsActive);
            if (course != null)
            {
                memberQuery = memberQuery.Where(u => u.Course == course);
            }
            if (filter.YearLevel.HasValue)
            {
                var year = (byte)filter.YearLevel.Value;
                memberQuery = memberQuery.Where(u => u.YearLevel == year);
            }
            var members = memberQuery
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.StudentNumber, u.Course, u.YearLevel })
                .ToList();
            var memberIds = members.Select(m => m.Id).ToHashSet();

            /* ---- Check-ins: those events × those members ---------------- */
            // Deactivated members' past check-ins are left out too, so every
            // figure on the page is about the same group of people.
            var rows = _attendance.GetAttendanceDetails()
                .Where(a => eventIds.Contains(a.EventId))
                .ToList()
                .Where(a => memberIds.Contains(a.UserId))
                .ToList();

            var durations = rows.Where(r => r.MinutesAttended.HasValue).Select(r => (double)r.MinutesAttended.Value).ToList();

            var report = new ReportViewModel
            {
                Filter = filter,
                MembersInScope = members.Count,
                EventsInScope = events.Count,
                CheckIns = rows.Count,
                Late = rows.Count(r => r.IsLate == 1),
                OnTime = rows.Count(r => r.IsLate != 1),
                MissingTimeOuts = rows.Count(r => r.TimeOut == null),
                Participants = rows.Select(r => r.UserId).Distinct().Count(),
                AverageMinutesAttended = durations.Count > 0 ? durations.Average() : null,
            };

            /* ---- Per event --------------------------------------------- */
            var byEvent = rows.ToLookup(r => r.EventId);
            report.PerEvent = events.Select(e => new EventTurnout
            {
                EventId = e.EventId,
                Name = e.EventName,
                Type = e.EventType,
                Start = e.EventStart,
                Status = e.EventStatus,
                Capacity = e.EventCapacity,
                OnTime = byEvent[e.EventId].Count(r => r.IsLate != 1),
                Late = byEvent[e.EventId].Count(r => r.IsLate == 1),
                MissingTimeOut = byEvent[e.EventId].Count(r => r.TimeOut == null),
            }).ToList();

            /* ---- By course and by year --------------------------------- */
            var byUser = rows.ToLookup(r => r.UserId);
            report.ByCourse = members
                .GroupBy(m => string.IsNullOrWhiteSpace(m.Course) ? "No course" : m.Course)
                .Select(g => new GroupParticipation
                {
                    Label = g.Key,
                    Members = g.Count(),
                    Participants = g.Count(m => byUser[m.Id].Any()),
                    CheckIns = g.Sum(m => byUser[m.Id].Count()),
                })
                .OrderByDescending(g => g.Rate).ThenBy(g => g.Label)
                .ToList();

            report.ByYear = members
                .GroupBy(m => m.YearLevel)
                .OrderBy(g => g.Key ?? 99)
                .Select(g => new GroupParticipation
                {
                    Label = YearLevels.Label(g.Key) ?? "No year",
                    Members = g.Count(),
                    Participants = g.Count(m => byUser[m.Id].Any()),
                    CheckIns = g.Sum(m => byUser[m.Id].Count()),
                })
                .ToList();

            /* ---- By event type ----------------------------------------- */
            report.ByType = report.PerEvent
                .GroupBy(e => e.Type)
                .Select(g => new TypeTurnout { Label = g.Key, Events = g.Count(), CheckIns = g.Sum(e => e.Total) })
                .OrderByDescending(t => t.Average)
                .ToList();

            /* ---- Arrival timing, relative to each event's start --------- */
            var buckets = new (string Label, double Upto)[]
            {
                ("Before start", 0), ("0–5 min", 5), ("5–15 min", 15), ("15–30 min", 30), ("30–60 min", 60), ("60+ min", double.MaxValue),
            };
            report.Arrivals = buckets.Select(b => new ArrivalBucket { Label = b.Label }).ToList();
            foreach (var r in rows)
            {
                var minutes = (r.TimeIn - r.EventStart).TotalMinutes;
                var i = Array.FindIndex(buckets, b => minutes < b.Upto || b.Upto == double.MaxValue);
                if (r.IsLate == 1) report.Arrivals[i].Late++; else report.Arrivals[i].OnTime++;
            }

            /* ---- Members table ----------------------------------------- */
            report.Members = members
                .Select(m =>
                {
                    var mine = byUser[m.Id].ToList();
                    return new MemberParticipation
                    {
                        UserId = m.Id,
                        FullName = $"{m.LastName}, {m.FirstName}",
                        StudentNumber = m.StudentNumber,
                        Course = m.Course,
                        YearLevel = m.YearLevel,
                        Attended = mine.Count,
                        Late = mine.Count(r => r.IsLate == 1),
                        MissingTimeOuts = mine.Count(r => r.TimeOut == null),
                        LastAttended = mine.Count > 0 ? mine.Max(r => r.TimeIn) : null,
                        Rate = events.Count > 0 ? (double)mine.Count / events.Count : 0,
                    };
                })
                .OrderByDescending(m => m.Attended).ThenBy(m => m.Late).ThenBy(m => m.FullName)
                .ToList();

            /* ---- Filter choices ---------------------------------------- */
            report.Courses = _users.GetUsers()
                .Where(u => u.IsActive && u.Course != null)
                .Select(u => u.Course).Distinct().OrderBy(c => c).ToList();
            report.EventTypes = _eventTypes.GetEventTypes()
                .OrderBy(t => t.TypeName)
                .Select(t => new LookupItem(t.Id, t.TypeName)).ToList();
            report.Events = _events.GetEventSummaries()
                .Where(e => e.EventStatus != Const.StatusUpcoming)
                .OrderByDescending(e => e.EventStart)
                .Select(e => new LookupItem(e.EventId, e.EventName)).ToList();

            return report;
        }
    }
}
