using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.ServiceModels
{
    // The add/edit form for a student. Limits mirror dbo.users.
    public class StudentViewModel
    {
        public int Id { get; set; }

        /* Letters, including ñ and accented ones, with the spaces, hyphens,
           periods and apostrophes real names use (Dela Cruz, Ma., O'Neil).
           Spelled as ranges rather than \p{L} so the browser's check reads it
           the same way the server does. */
        public const string NamePattern = @"^[A-Za-zÀ-ÖØ-öø-ÿ][A-Za-zÀ-ÖØ-öø-ÿ .'-]*$";

        [Required(ErrorMessage = "Student number is required.")]
        [StringLength(20)]
        [RegularExpression(@"^\d{4}-\d{5}$", ErrorMessage = "Use the format 2026-00001.")]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [RegularExpression(NamePattern, ErrorMessage = "Use letters only (spaces, - . and ' are fine).")]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [RegularExpression(NamePattern, ErrorMessage = "Use letters only (spaces, - . and ' are fine).")]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; }

        [StringLength(100)]
        [RegularExpression(@"^[A-Za-z][A-Za-z .&-]*$", ErrorMessage = "Use letters only, like BSIT or Information Technology.")]
        public string Course { get; set; }

        // 1 to 5, or 0 for a non-blocked (irregular) student; see YearLevels.
        [Range(0, 5, ErrorMessage = "Year level is 1 to 5, or Non-blocked.")]
        [Display(Name = "Year level")]
        public int? YearLevel { get; set; }

        [RegularExpression(@"^\+639\d{9}$", ErrorMessage = "Use the format +639XXXXXXXXX.")]
        [Display(Name = "Contact number")]
        public string ContactNumber { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class StudentListItem
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Course { get; set; }
        public byte? YearLevel { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public int EventsAttended { get; set; }
    }

    public class StudentListViewModel
    {
        public string Search { get; set; }
        public bool ShowInactive { get; set; }
        public List<StudentListItem> Students { get; set; } = new();
    }

    public class StudentDetailViewModel
    {
        public StudentViewModel Student { get; set; }
        public int EventsAttended { get; set; }
        public int TimesLate { get; set; }
        public int MissingTimeOuts { get; set; }
        public List<AttendanceDetail> History { get; set; } = new();

        // The member's sign-in, if they have one.
        public int? AccountId { get; set; }
        public string AccountUsername { get; set; }
        public bool AccountActive { get; set; }
        public System.DateTime? LastLogin { get; set; }
    }
}
