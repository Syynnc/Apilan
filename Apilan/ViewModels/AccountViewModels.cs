using System.ComponentModel.DataAnnotations;
using Apilan.Models;

namespace Apilan.ViewModels;

/// <summary>
/// Sign-in form. The single identifier field accepts either an email address
/// or a student number, which is what the HLD calls for.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email or student number.")]
    [Display(Name = "Email or student number")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Account request. Creates a Pending account plus the member details an
/// officer needs in order to decide, but grants no role until approval.
/// </summary>
public class RegisterViewModel
{
    [Required, StringLength(80)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Student number")]
    [RegularExpression(@"^[0-9]{4}-[0-9]{4,6}$", ErrorMessage = "Use the format 2024-01234.")]
    public string StudentNumber { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    [Display(Name = "School email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Course { get; set; } = string.Empty;

    [Required, Range(1, 6)]
    [Display(Name = "Year level")]
    public int YearLevel { get; set; } = 1;

    [StringLength(20)]
    public string? Section { get; set; }

    [Phone, StringLength(30)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>One row on the account approval queue.</summary>
public class AccountRequestViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Course { get; set; }
    public int? YearLevel { get; set; }
    public string? Section { get; set; }
    public string? ContactNumber { get; set; }
    public DateTime RequestedOn { get; set; }
    public AccountStatus Status { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>True when a member record already carries this student number.</summary>
    public bool MatchesExistingMember { get; set; }

    public string StatusChip => Status switch
    {
        AccountStatus.Pending => "warn",
        AccountStatus.Active => "ok",
        AccountStatus.Rejected => "risk",
        AccountStatus.Disabled => "idle",
        _ => "idle"
    };

    public string CourseYear =>
        string.IsNullOrWhiteSpace(Course) ? "—" : $"{Course} · {YearLevel}";
}

/// <summary>The approval queue plus its filter state.</summary>
public class AccountRequestListViewModel
{
    public IReadOnlyList<AccountRequestViewModel> Requests { get; set; } = Array.Empty<AccountRequestViewModel>();
    public AccountStatus? Filter { get; set; }
    public int PendingCount { get; set; }
}
