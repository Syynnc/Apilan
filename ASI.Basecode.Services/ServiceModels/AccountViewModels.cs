using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>What the sign-in cookie needs to know about an account.</summary>
    public class SignedInAccount
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public string DisplayName { get; set; }

        /// <summary>The linked student (dbo.users) id, when this is a member account.</summary>
        public int? UserId { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Enter a new password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm the new password.")]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; }
    }

    public class AccountViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50)]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Letters, digits, dot, dash and underscore only.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm the password.")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Choose a role.")]
        [Display(Name = "Role")]
        public int? RoleId { get; set; }

        [Display(Name = "Linked student")]
        public int? UserId { get; set; }
    }

    public class AccountListItem
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public string StudentName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class AccountsPageViewModel
    {
        public List<AccountListItem> Accounts { get; set; } = new();
        public AccountViewModel NewAccount { get; set; } = new();
        public List<LookupItem> Roles { get; set; } = new();
        public List<LookupItem> Students { get; set; } = new();
    }
}
