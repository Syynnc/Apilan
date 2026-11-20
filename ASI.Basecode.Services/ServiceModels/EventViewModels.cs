using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Messages;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.ServiceModels
{
    // The add/edit form for an event. Limits mirror dbo.events.
    public class EventViewModel : IValidatableObject
    {
        public int Id { get; set; }

        /* Who hosts an event, or what a type is called: letters (ñ and accents
           too) with spaces and - & . ' (Alliance Executive Board, Team-building). */
        public const string GroupNamePattern = @"^[A-Za-zÀ-ÖØ-öø-ÿ][A-Za-zÀ-ÖØ-öø-ÿ .&'-]*$";

        [Required(ErrorMessage = "Event name is required.")]
        [StringLength(255)]
        [Display(Name = "Event name")]
        public string EventName { get; set; }

        [Required(ErrorMessage = "Choose an event type.")]
        [Display(Name = "Event type")]
        public int? EventTypeId { get; set; }

        [Required(ErrorMessage = "Start is required.")]
        [Display(Name = "Starts")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime? EventStart { get; set; }

        [Required(ErrorMessage = "End is required.")]
        [Display(Name = "Ends")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime? EventEnd { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Venue is required.")]
        [StringLength(255)]
        public string Venue { get; set; }

        [Range(1, 100000, ErrorMessage = "Capacity must be at least 1.")]
        [Display(Name = "Capacity")]
        public int? EventCapacity { get; set; }

        [StringLength(255)]
        [RegularExpression(GroupNamePattern, ErrorMessage = "Use letters only (spaces and - & . ' are fine).")]
        [Display(Name = "Host")]
        public string EventHost { get; set; }

        [Range(15, 30, ErrorMessage = "The window is 15 to 30 minutes.")]
        [Display(Name = "Time-in / time-out window")]
        public int AttendanceWindowMinutes { get; set; } = 15;

        public List<LookupItem> EventTypes { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EventStart.HasValue && EventEnd.HasValue && EventEnd <= EventStart)
            {
                yield return new ValidationResult("The event must end after it starts.", new[] { nameof(EventEnd) });
            }
            else if (EventStart.HasValue && EventEnd.HasValue
                     && (EventEnd.Value - EventStart.Value).TotalMinutes < AttendanceWindowMinutes)
            {
                // Otherwise the time-out window would open before time-in closed.
                yield return new ValidationResult(Errors.WindowLongerThanEvent, new[] { nameof(AttendanceWindowMinutes) });
            }

        }
    }

    public class EventListViewModel
    {
        public string Status { get; set; }
        public string Search { get; set; }
        public List<EventSummary> Events { get; set; } = new();
        public int UpcomingCount { get; set; }
        public int OngoingCount { get; set; }
        public int FinishedCount { get; set; }
    }

    public class EventDetailViewModel
    {
        public EventSummary Summary { get; set; }
        public string Description { get; set; }
        public AttendanceWindow Window { get; set; }
        public List<AttendanceDetail> Attendance { get; set; } = new();
        public List<EventAbsentee> Absentees { get; set; } = new();
        public List<LookupItem> Modes { get; set; } = new();
        public AttendanceEntryViewModel Entry { get; set; } = new();
    }

    // One time-in or time-out typed or scanned at the door.
    public class AttendanceEntryViewModel
    {
        public int EventId { get; set; }

        [Required(ErrorMessage = "Enter or scan a student number.")]
        [StringLength(20)]
        // A handheld scanner may add a space or newline; the service trims it.
        [RegularExpression(@"^\s*\d{4}-\d{5}\s*$", ErrorMessage = "Student numbers look like 2026-00001.")]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; }

        [Display(Name = "Mode")]
        public int AttendanceModeId { get; set; }

        // Leave empty for "now"; set it to record a late-entered paper sign-in.
        [Display(Name = "Recorded time")]
        public DateTime? RecordedAt { get; set; }
    }

    public class EventTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(50)]
        [RegularExpression(EventViewModel.GroupNamePattern, ErrorMessage = "Use letters only (spaces and - & . ' are fine).")]
        [Display(Name = "Name")]
        public string TypeName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        [Display(Name = "Colour")]
        public string Color { get; set; }
    }
}
