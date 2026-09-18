using System.ComponentModel.DataAnnotations;
using Apilan.Models;

namespace Apilan.ViewModels;

// ===========================================================================
//  MEMBERS
// ===========================================================================

/// <summary>The member roster with its search and filter state.</summary>
public class MemberListViewModel
{
    public List<MemberRowViewModel> Members { get; set; } = new();

    public string? Search { get; set; }
    public MembershipStatus? Status { get; set; }
    public MembershipType? Type { get; set; }
    public string? Course { get; set; }

    /// <summary>Courses present on the roster, for the filter dropdown.</summary>
    public List<string> Courses { get; set; } = new();

    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
}

public class MemberRowViewModel
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public int YearLevel { get; set; }
    public string? Section { get; set; }
    public string? Email { get; set; }
    public string? ContactNumber { get; set; }
    public MembershipType MembershipType { get; set; }
    public MembershipStatus Status { get; set; }
    public DateTime JoinedOn { get; set; }
    public int Registrations { get; set; }
    public bool HasAccount { get; set; }

    public string CourseYear => $"{Course} · {YearLevel}";
    public string StatusChip => Status == MembershipStatus.Active ? "ok" : "idle";
}

/// <summary>Add or edit a member record.</summary>
public class MemberFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Student number")]
    [RegularExpression(@"^[0-9]{4}-[0-9]{4,6}$", ErrorMessage = "Use the format 2024-01234.")]
    public string StudentNumber { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [Required, StringLength(60)]
    public string Course { get; set; } = string.Empty;

    [Required, Range(1, 6)]
    [Display(Name = "Year level")]
    public int YearLevel { get; set; } = 1;

    [StringLength(20)]
    public string? Section { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [Phone, StringLength(30)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [Display(Name = "Membership type")]
    public MembershipType MembershipType { get; set; } = MembershipType.Regular;

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    [Display(Name = "Joined on")]
    [DataType(DataType.Date)]
    public DateTime JoinedOn { get; set; } = DateTime.Today;

    public bool IsNew => Id == 0;

    public static MemberFormViewModel FromEntity(Member m) => new()
    {
        Id = m.Id,
        StudentNumber = m.StudentNumber,
        LastName = m.LastName,
        FirstName = m.FirstName,
        MiddleName = m.MiddleName,
        Course = m.Course,
        YearLevel = m.YearLevel,
        Section = m.Section,
        Email = m.Email,
        ContactNumber = m.ContactNumber,
        MembershipType = m.MembershipType,
        Status = m.Status,
        JoinedOn = m.JoinedOn
    };

    public void ApplyTo(Member m)
    {
        m.StudentNumber = StudentNumber.Trim();
        m.LastName = LastName.Trim();
        m.FirstName = FirstName.Trim();
        m.MiddleName = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim();
        m.Course = Course.Trim().ToUpperInvariant();
        m.YearLevel = YearLevel;
        m.Section = string.IsNullOrWhiteSpace(Section) ? null : Section.Trim().ToUpperInvariant();
        m.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        m.ContactNumber = string.IsNullOrWhiteSpace(ContactNumber) ? null : ContactNumber.Trim();
        m.MembershipType = MembershipType;
        m.Status = Status;
        m.JoinedOn = JoinedOn;
    }
}

/// <summary>A member's record with the history behind it.</summary>
public class MemberDetailsViewModel
{
    public Member Member { get; set; } = null!;
    public bool HasAccount { get; set; }
    public AccountStatus? AccountStatus { get; set; }
    public string? AccountEmail { get; set; }

    public List<MemberHistoryRow> History { get; set; } = new();

    public int EventsAttended => History.Count(h => h.Attended > 0);
    public int CertificatesEarned => History.Count(h => h.Earned);
}

public class MemberHistoryRow
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public int Attended { get; set; }
    public int Sessions { get; set; }
    public EligibilityStatus Eligibility { get; set; }
    public bool Earned { get; set; }
}

// ===========================================================================
//  EVENT TYPES
// ===========================================================================

public class EventTypeFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    /// <summary>One of the design system's five spine colours.</summary>
    [Required, StringLength(20)]
    [Display(Name = "Colour key")]
    public string ColorKey { get; set; } = "talk";

    [Display(Name = "In use")]
    public bool IsActive { get; set; } = true;

    public int EventCount { get; set; }

    public bool IsNew => Id == 0;

    /// <summary>The colour keys the ticket and calendar components understand.</summary>
    public static readonly (string Key, string Label)[] Colors =
    {
        ("talk", "Talk — ink blue"),
        ("workshop", "Workshop — clay"),
        ("assembly", "Assembly — plum"),
        ("service", "Service — moss"),
        ("training", "Training — amber")
    };
}

// ===========================================================================
//  VENUES
// ===========================================================================

public class VenueFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Room / venue name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Location { get; set; }

    [Required, Range(1, 100000)]
    public int Capacity { get; set; } = 50;

    [StringLength(300)]
    [Display(Name = "Availability notes")]
    public string? AvailabilityNotes { get; set; }

    [StringLength(120)]
    [Display(Name = "Contact person")]
    public string? ContactPerson { get; set; }

    [Phone, StringLength(30)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [Display(Name = "Bookable")]
    public bool IsActive { get; set; } = true;

    public int UpcomingEvents { get; set; }

    public bool IsNew => Id == 0;
}
