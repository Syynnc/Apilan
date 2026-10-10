using System;

namespace ASI.Basecode.Data.Models
{
    /// <summary>dbo.attendance — one row per student per event.</summary>
    public partial class Attendance
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public int AttendanceModeId { get; set; }
        public DateTime TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedTime { get; set; }

        public virtual User User { get; set; }
        public virtual Event Event { get; set; }
        public virtual AttendanceMode AttendanceMode { get; set; }
    }
}
