using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>Every account, with the role it holds and the record behind it.</summary>
public class OfficerListViewModel
{
    public List<AccountRowViewModel> Accounts { get; set; } = new();

    public string? Role { get; set; }
    public string? Search { get; set; }

    public int Admins { get; set; }
    public int Officers { get; set; }
    public int Members { get; set; }

    /// <summary>Members without an account, for the "give someone an account" picker.</summary>
    public List<MemberOption> UnlinkedMembers { get; set; } = new();
}

public class AccountRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public AccountStatus Status { get; set; }
    public string Role { get; set; } = Roles.Member;
    public int? MemberId { get; set; }
    public string? MemberName { get; set; }
    public DateTime RequestedOn { get; set; }
    public DateTime? ReviewedOn { get; set; }

    /// <summary>True for the signed-in user, so the screen can refuse self-harm.</summary>
    public bool IsSelf { get; set; }

    public string StatusChip => Status switch
    {
        AccountStatus.Active => "ok",
        AccountStatus.Pending => "info",
        AccountStatus.Rejected => "risk",
        _ => "idle"
    };

    public string RoleChip => Role switch
    {
        Roles.Admin => "risk",
        Roles.Officer => "info",
        _ => "idle"
    };
}

/// <summary>The activity log, filtered and paged.</summary>
public class AuditLogViewModel
{
    public List<AuditRow> Rows { get; set; } = new();

    public string? Action { get; set; }
    public string? EntityName { get; set; }

    /// <summary>Narrowed to one record's own trail, when arrived at from that record.</summary>
    public string? EntityId { get; set; }

    public string? ActorId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<string> Actions { get; set; } = new();
    public List<string> Entities { get; set; } = new();
    public List<(string Id, string Name)> Actors { get; set; } = new();

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int Total { get; set; }

    public int Pages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < Pages;
    public int FirstOnPage => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastOnPage => Math.Min(Page * PageSize, Total);
}

public class AuditRow
{
    public int Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Detail { get; set; }

    /// <summary>Colour by the kind of thing that happened, not by entity.</summary>
    public string Chip => Action switch
    {
        var a when a.Contains("Cancel", StringComparison.Ordinal)
                || a.Contains("Reject", StringComparison.Ordinal)
                || a.Contains("Revoked", StringComparison.Ordinal)
                || a.Contains("Removed", StringComparison.Ordinal) => "risk",
        var a when a.Contains("Approved", StringComparison.Ordinal)
                || a.Contains("Published", StringComparison.Ordinal)
                || a.Contains("Granted", StringComparison.Ordinal)
                || a.Contains("Created", StringComparison.Ordinal) => "ok",
        var a when a.Contains("Role", StringComparison.Ordinal)
                || a.Contains("Disabled", StringComparison.Ordinal) => "warn",
        _ => "idle"
    };
}
