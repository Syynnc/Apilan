using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    /// <summary>A student (dbo.users). Never hard-deleted; deactivated instead.</summary>
    public partial class User
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Gender { get; set; }
        public string Email { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
        public string ContactNumber { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedTime { get; set; }

        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public virtual Account Account { get; set; }
    }
}
