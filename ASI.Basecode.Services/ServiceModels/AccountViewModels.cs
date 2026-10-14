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

        /// <summary>Signed in with the default password, so it has to be changed first.</summary>
        public bool MustChangePassword { get; set; }
    }

    public class ChangePasswordViewModel
    {
        /// <summary>
        /// Not asked for on a first sign-in: the account is still on the
        /// default password, which the server already knows.
        /// </summary>
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

        /// <summary>Set when the form is the forced first-sign-in prompt.</summary>
        public bool FirstSignIn { get; set; }
    }

    public class AccountListItem
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public int? UserId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public bool StudentActive { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class AccountsPageViewModel
    {
        public List<AccountListItem> Accounts { get; set; } = new();

        /// <summary>Shown in the reset prompt, so the officer can tell the member what it is.</summary>
        public string DefaultPassword { get; set; }

        /// <summary>Active members who have no sign-in yet.</summary>
        public int MembersWithoutAccount { get; set; }
    }
}
