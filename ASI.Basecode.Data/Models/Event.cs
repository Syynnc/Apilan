using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    /// <summary>dbo.events</summary>
    public partial class Event
    {
        public int Id { get; set; }
        public string EventName { get; set; }
        public string Description { get; set; }
        public string Venue { get; set; }
        public int? EventCapacity { get; set; }
        public string EventHost { get; set; }
        public int EventTypeId { get; set; }
        public DateTime EventStart { get; set; }
        public DateTime EventEnd { get; set; }
        /// <summary>Always equal to the window: arriving after it is late (database/06).</summary>
        public int LateGraceMinutes { get; set; }

        /// <summary>
        /// Minutes (15 to 30) that time-in stays open from the start, and
        /// time-out from the end.
        /// </summary>
        public int AttendanceWindowMinutes { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedTime { get; set; }

        public virtual EventType EventType { get; set; }
        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
