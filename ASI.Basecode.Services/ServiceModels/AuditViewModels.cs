using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>One thing someone did, to be written to the activity log.</summary>
    public class AuditEntry
    {
        public int? AccountId { get; set; }
        public string Actor { get; set; }
        public string Action { get; set; }
        public string TargetType { get; set; }
        public int? TargetId { get; set; }
        public string TargetLabel { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
    }

    public class ActivityFilter
    {
        /// <summary>accounts.id of the person who did it.</summary>
        public int? Who { get; set; }

        /// <summary>One of AuditActions.Groups.</summary>
        public string Group { get; set; }

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public bool IsEmpty => Who == null && string.IsNullOrEmpty(Group) && From == null && To == null;
    }

    public class ActivityRow
    {
        public long Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public int? AccountId { get; set; }
        public string Actor { get; set; }
        public string ActorName { get; set; }
        public string ActorRole { get; set; }
        public string Action { get; set; }
        public string TargetType { get; set; }
        public int? TargetId { get; set; }
        public string TargetLabel { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
    }

    public class ActivityPageViewModel
    {
        public ActivityFilter Filter { get; set; } = new();
        public List<ActivityRow> Rows { get; set; } = new();

        /// <summary>How many matched; only the newest <see cref="Limit"/> are listed.</summary>
        public int Total { get; set; }
        public int Limit { get; set; }

        public int Today { get; set; }
        public int Officers { get; set; }
        public DateTime? Latest { get; set; }

        public List<LookupItem> People { get; set; } = new();
    }
}
