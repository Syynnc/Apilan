using System.ComponentModel.DataAnnotations;
using Apilan.Models;

namespace Apilan.ViewModels;

// ===========================================================================
//  ORGANIZATIONS
// ===========================================================================

/// <summary>The directory of student organizations, with how busy each one is.</summary>
public class OrganizationListViewModel
{
    public List<OrganizationRow> Organizations { get; set; } = new();

    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }

    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }

    public int TotalEvents => Organizations.Sum(o => o.TotalEvents);
}

public class OrganizationRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Acronym { get; set; } = string.Empty;
    public string? Adviser { get; set; }
    public string? ContactEmail { get; set; }
    public string ColorKey { get; set; } = "talk";
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }

    public int TotalEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int Officers { get; set; }

    /// <summary>Slots held across everything the org has ever run.</summary>
    public int Registrations { get; set; }

    public string StatusChip => IsActive ? "ok" : "idle";
}

/// <summary>Add or edit an organization.</summary>
public class OrganizationFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(140)]
    [Display(Name = "Organization name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Acronym")]
    public string Acronym { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(120)]
    [Display(Name = "Faculty adviser")]
    public string? Adviser { get; set; }

    [EmailAddress, StringLength(120)]
    [Display(Name = "Contact email")]
    public string? ContactEmail { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Colour key")]
    public string ColorKey { get; set; } = "talk";

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsNew => Id == 0;

    public static OrganizationFormViewModel FromEntity(Organization o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Acronym = o.Acronym,
        Description = o.Description,
        Adviser = o.Adviser,
        ContactEmail = o.ContactEmail,
        ColorKey = o.ColorKey,
        IsActive = o.IsActive
    };

    public void ApplyTo(Organization o)
    {
        o.Name = Name.Trim();
        o.Acronym = Acronym.Trim().ToUpperInvariant();
        o.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        o.Adviser = string.IsNullOrWhiteSpace(Adviser) ? null : Adviser.Trim();
        o.ContactEmail = string.IsNullOrWhiteSpace(ContactEmail) ? null : ContactEmail.Trim();
        o.ColorKey = ColorKey;
        o.IsActive = IsActive;
    }
}

/// <summary>
/// One organization and everything it has run — the answer to "show me this
/// org's events", which is the whole point of the module.
/// </summary>
public class OrganizationDetailsViewModel
{
    public Organization Organization { get; set; } = null!;

    public List<EventRowViewModel> Events { get; set; } = new();
    public List<OrganizationOfficerRow> Officers { get; set; } = new();

    public EventStatus? Status { get; set; }

    public int TotalEvents { get; set; }
    public int Registrations { get; set; }
    public int Attended { get; set; }
    public int Certificates { get; set; }

    public int TurnoutPercent => Registrations <= 0
        ? 0
        : (int)Math.Round(Attended * 100.0 / Registrations);

    /// <summary>Average of every feedback response across the org's events.</summary>
    public double? AverageRating { get; set; }
    public int Responses { get; set; }
}

public record OrganizationOfficerRow(string UserId, string FullName, string? Email, string Role);

/// <summary>An organization as a dropdown entry on the event form.</summary>
public record OrganizationOption(int Id, string Name, string Acronym, string ColorKey);
