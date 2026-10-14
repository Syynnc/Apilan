using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public class ReportService : ServiceBase, IReportService
    {
        private readonly IEventRepository _events;
        private readonly IUserRepository _users;
        private readonly IAttendanceRepository _attendance;

        public ReportService(IEventRepository events, IUserRepository users, IAttendanceRepository attendance)
        {
            _events = events;
            _users = users;
            _attendance = attendance;
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

        public ReportViewModel GetReport(string course)
        {
            var students = _attendance.GetStudentSummaries();
            if (!string.IsNullOrWhiteSpace(course))
            {
                students = students.Where(s => s.Course == course);
            }

            return new ReportViewModel
            {
                Course = course,
                Courses = _users.GetUsers()
                    .Where(u => u.IsActive && u.Course != null)
                    .Select(u => u.Course)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList(),
                Events = _events.GetEventSummaries().OrderByDescending(e => e.EventStart).ToList(),
                Students = students
                    .OrderByDescending(s => s.EventsAttended)
                    .ThenBy(s => s.FullName)
                    .ToList(),
            };
        }
    }
}
