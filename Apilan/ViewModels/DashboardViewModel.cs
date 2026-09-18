using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>
/// The officer console home. It answers one question — what needs doing today
/// — so every block on it is either a queue or a door.
/// </summary>
public class DashboardViewModel
{
    public int PublishedEvents { get; set; }
    public int DraftEvents { get; set; }
    public int ActiveMembers { get; set; }
    public int PendingAccounts { get; set; }
    public int PendingRegistrations { get; set; }
    public int WaitlistedTotal { get; set; }
    public int CertificatesEarned { get; set; }

    /// <summary>Doors that can be opened right now.</summary>
    public List<DashboardEventRow> Today { get; set; } = new();

    public List<DashboardEventRow> Upcoming { get; set; } = new();

    /// <summary>Finished events that have not been closed out yet.</summary>
    public List<DashboardEventRow> AwaitingCloseOut { get; set; } = new();

    public List<AuditRow> Recent { get; set; } = new();

    public bool HasAnythingToDo =>
        PendingAccounts > 0 || PendingRegistrations > 0 || Today.Any() || AwaitingCloseOut.Any();
}

public class DashboardEventRow
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public EventStatus Status { get; set; }
    public int Capacity { get; set; }
    public int Taken { get; set; }
    public int Present { get; set; }
    public int Sessions { get; set; }

    public int FillPercent => Capacity <= 0 ? 0 : Math.Min(100, (int)Math.Round(Taken * 100.0 / Capacity));
    public bool IsRunningNow => DateTime.Now >= StartsAt.AddHours(-1) && DateTime.Now <= EndsAt;
}
