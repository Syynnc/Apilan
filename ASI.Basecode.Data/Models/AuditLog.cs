using System;

namespace ASI.Basecode.Data.Models
{
    // dbo.audit_logs — one row per change, added and never edited.
    public partial class AuditLog
    {
        public long Id { get; set; }

        // Set by the database (SYSDATETIME), to the millisecond.
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
