using System;

namespace ASI.Basecode.Data.Models
{
    /// <summary>A sign-in account (dbo.accounts), optionally linked to a student.</summary>
    public partial class Account
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public int RoleId { get; set; }
        public int? UserId { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedTime { get; set; }

        public virtual Role Role { get; set; }
        public virtual User User { get; set; }
    }
}
