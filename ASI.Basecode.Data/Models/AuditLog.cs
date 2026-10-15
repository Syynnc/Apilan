using System;

namespace ASI.Basecode.Data.Models
{
    /// <summary>dbo.audit_logs — one row per change, added and never edited.</summary>
    public partial class AuditLog
    {
        public long Id { get; set; }

        /// <summary>Set by the database (SYSDATETIME), to the millisecond.</summary>
        public DateTime OccurredAt { get; set; }

        public int? AccountId { get; set; }
        public string Actor { get; set; }
        public string Action { get; set; }
        public string TargetType { get; set; }
        public int? TargetId { get; set; }
        public string TargetLabel { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }

        public virtual Account Account { get; set; }
    }
}
