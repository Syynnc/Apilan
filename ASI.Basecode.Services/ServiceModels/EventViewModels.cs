using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Messages;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>The add/edit form for an event. Limits mirror dbo.events.</summary>
    public class EventViewModel : IValidatableObject
    {
        public int Id { get; set; }

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

    /// <summary>One time-in or time-out typed or scanned at the door.</summary>
    public class AttendanceEntryViewModel
    {
        public int EventId { get; set; }

        [Required(ErrorMessage = "Enter or scan a student number.")]
        [StringLength(20)]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; }

        [Display(Name = "Mode")]
        public int AttendanceModeId { get; set; }

        /// <summary>Leave empty for "now"; set it to record a late-entered paper sign-in.</summary>
        [Display(Name = "Recorded time")]
        public DateTime? RecordedAt { get; set; }
    }

    public class EventTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(50)]
        [Display(Name = "Name")]
        public string TypeName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
