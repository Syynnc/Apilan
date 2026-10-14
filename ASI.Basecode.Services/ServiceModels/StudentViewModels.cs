using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>The add/edit form for a student. Limits mirror dbo.users.</summary>
    public class StudentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Student number is required.")]
        [StringLength(20)]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; }

        [StringLength(100)]
        public string Course { get; set; }

        [Range(1, 6, ErrorMessage = "Year level is 1 to 6.")]
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
    }
}
