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
    }

    public class ReportViewModel
    {
        public string Course { get; set; }
        public List<string> Courses { get; set; } = new();
        public List<EventSummary> Events { get; set; } = new();
        public List<StudentSummary> Students { get; set; } = new();
    }
}
