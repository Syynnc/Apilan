using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Apilan.Models;

/// <summary>
/// Login account. Identity owns the credentials; the organization's own record
/// of the person lives on <see cref="Member"/>, which an account may or may not
/// be linked to — officers can keep member records for people who never sign up
/// online, and walk-ins are registered at the door without an account.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Alternate login identifier. Null for staff without one.</summary>
    [StringLength(20)]
    public string? StudentNumber { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Pending;

    public DateTime RequestedOn { get; set; } = DateTime.Now;
    public DateTime? ReviewedOn { get; set; }

    public string? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }

    /// <summary>Why an account request was turned down. Shown to the applicant.</summary>
    [StringLength(300)]
    public string? ReviewNote { get; set; }

    public int? MemberId { get; set; }
    public Member? Member { get; set; }

    /// <summary>
    /// The organization this account acts for. Officers get their own org's
    /// name in the console shell and it pre-fills on the event form; admins
    /// are campus-wide and may have none.
    /// </summary>
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

// ===========================================================================
//  MASTER DATA
// ===========================================================================

/// <summary>
/// A student organization. Events belong to one, which is what makes
/// "the events this org ran" a question the system can answer.
/// </summary>
public class Organization
{
    public int Id { get; set; }

    [Required, StringLength(140)]
    [Display(Name = "Organization name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Acronym { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(120)]
    public string? Adviser { get; set; }

    [EmailAddress, StringLength(120)]
    [Display(Name = "Contact email")]
    public string? ContactEmail { get; set; }

    /// <summary>Same five-key palette the ticket spine and calendar pips use.</summary>
    [Required, StringLength(20)]
    [Display(Name = "Colour key")]
    public string ColorKey { get; set; } = "talk";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedOn { get; set; } = DateTime.Now;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

/// <summary>Master record for a person in the organization.</summary>
public class Member
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Student number")]
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

    [Range(1, 6)]
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

    public DateTime JoinedOn { get; set; } = DateTime.Now;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    [NotMapped]
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName![0]}. {LastName}";

    [NotMapped]
    public string ListName => $"{LastName}, {FirstName}";

    [NotMapped]
    public string CourseYear => $"{Course} · {YearLevel}";
}

/// <summary>Standard activity classification — seminar, outreach, training, and so on.</summary>
public class EventType
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// Maps to a ticket spine / calendar pip colour in the design system:
    /// talk, workshop, assembly, service, training.
    /// </summary>
    [Required, StringLength(20)]
    [Display(Name = "Colour key")]
    public string ColorKey { get; set; } = "talk";

    public bool IsActive { get; set; } = true;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

/// <summary>A room or place an event can be held in.</summary>
public class Venue
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Room / venue name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Location { get; set; }

    [Range(1, 100000)]
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

    public bool IsActive { get; set; } = true;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

// ===========================================================================
//  TRANSACTIONS
// ===========================================================================

/// <summary>An activity the organization runs, with one or more sessions.</summary>
public class Event
{
    public int Id { get; set; }

    [Required, StringLength(140)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Organization")]
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    [Display(Name = "Event type")]
    public int EventTypeId { get; set; }
    public EventType? EventType { get; set; }

    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    [Display(Name = "Starts")]
    public DateTime StartsAt { get; set; }

    [Display(Name = "Ends")]
    public DateTime EndsAt { get; set; }

    [Display(Name = "Registration deadline")]
    public DateTime RegistrationDeadline { get; set; }

    [Range(1, 100000)]
    public int Capacity { get; set; } = 50;

    /// <summary>When true, registrations land as Pending and need officer approval.</summary>
    [Display(Name = "Require approval")]
    public bool RequiresApproval { get; set; }

    /// <summary>When capacity is reached, further registrations are waitlisted.</summary>
    [Display(Name = "Allow waitlist")]
    public bool AllowWaitlist { get; set; } = true;

    /// <summary>
    /// Share of sessions a participant must attend to earn a certificate.
    /// Officers can still override per participant.
    /// </summary>
    [Range(0, 100)]
    [Display(Name = "Attendance required for certificate (%)")]
    public int CertificateThresholdPercent { get; set; } = 80;

    [Display(Name = "Issues certificates")]
    public bool IssuesCertificate { get; set; } = true;

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime? PublishedOn { get; set; }

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    /// <summary>
    /// Last edit, denormalised onto the row. The full history is in
    /// <see cref="AuditLog"/>; this is so the event screen can say who
    /// touched it last without a second query.
    /// </summary>
    public DateTime? LastEditedOn { get; set; }

    public string? LastEditedByUserId { get; set; }
    public ApplicationUser? LastEditedBy { get; set; }

    /// <summary>Collects feedback from participants once the event is over.</summary>
    [Display(Name = "Collect feedback")]
    public bool CollectsFeedback { get; set; } = true;

    public ICollection<EventSession> Sessions { get; set; } = new List<EventSession>();
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}

/// <summary>
/// One attendance-taking block within an event. A single-day event has one;
/// a two-day seminar might have four.
/// </summary>
public class EventSession
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Session name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Starts")]
    public DateTime StartsAt { get; set; }

    [Display(Name = "Ends")]
    public DateTime EndsAt { get; set; }

    /// <summary>Display order within the event.</summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Grace period after the session starts. Someone checked in past it is
    /// marked Late rather than Present. Zero means the bell is the bell.
    /// </summary>
    [Range(0, 240)]
    [Display(Name = "Late after (minutes)")]
    public int LateAfterMinutes { get; set; } = 15;

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    /// <summary>When someone arriving now stops counting as on time.</summary>
    [NotMapped]
    public DateTime LateThreshold => StartsAt.AddMinutes(LateAfterMinutes);
}

/// <summary>A member's place in an event, and the stub they present at the door.</summary>
public class Registration
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int MemberId { get; set; }
    public Member? Member { get; set; }

    /// <summary>Short human-typeable check-in code, e.g. APL-7K2M.</summary>
    [Required, StringLength(16)]
    public string Code { get; set; } = string.Empty;

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

    public DateTime RegisteredOn { get; set; } = DateTime.Now;

    /// <summary>Position in the queue when waitlisted; null otherwise.</summary>
    public int? WaitlistPosition { get; set; }

    /// <summary>True when an officer added this person at the door.</summary>
    [Display(Name = "Walk-in")]
    public bool IsWalkIn { get; set; }

    [StringLength(300)]
    public string? Remarks { get; set; }

    public EligibilityStatus Eligibility { get; set; } = EligibilityStatus.NotEligible;

    /// <summary>Set when an officer overrides the computed eligibility.</summary>
    [StringLength(300)]
    public string? EligibilityNote { get; set; }

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    /// <summary>The participant's feedback, once they have left some.</summary>
    public EventFeedback? Feedback { get; set; }
}

/// <summary>
/// One participant's presence at one session — present, late, or explicitly
/// absent. <see cref="TimeIn"/> is null on an absence: there is no arrival to
/// record, only the fact that the officer accounted for the person.
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    public int EventSessionId { get; set; }
    public EventSession? EventSession { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [Display(Name = "Time in")]
    public DateTime? TimeIn { get; set; }

    [Display(Name = "Time out")]
    public DateTime? TimeOut { get; set; }

    [Display(Name = "Recorded by")]
    public CheckInMethod Method { get; set; } = CheckInMethod.ManualSearch;

    /// <summary>Why an officer changed the status by hand.</summary>
    [StringLength(200)]
    public string? Note { get; set; }

    public string? RecordedByUserId { get; set; }
    public ApplicationUser? RecordedBy { get; set; }

    /// <summary>Present or late — anything that is not a no-show.</summary>
    [NotMapped]
    public bool Attended => Status != AttendanceStatus.Absent;
}

/// <summary>
/// One participant's verdict on one event, tied to their registration so a
/// person can only speak once and only about something they signed up for.
/// </summary>
public class EventFeedback
{
    public int Id { get; set; }

    public int RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    [Range(1, 5)]
    [Display(Name = "Overall")]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    [Display(Name = "Content and speakers")]
    public int ContentRating { get; set; }

    [Range(1, 5)]
    [Display(Name = "Venue and facilities")]
    public int VenueRating { get; set; }

    [Range(1, 5)]
    [Display(Name = "How well it was run")]
    public int OrganizationRating { get; set; }

    [Display(Name = "Would recommend this to a batchmate")]
    public bool WouldRecommend { get; set; } = true;

    [StringLength(1000)]
    [Display(Name = "What worked")]
    public string? Comment { get; set; }

    [StringLength(1000)]
    [Display(Name = "What to improve")]
    public string? Improvement { get; set; }

    /// <summary>Hides the name from officers reading the responses.</summary>
    [Display(Name = "Submit without my name")]
    public bool IsAnonymous { get; set; }

    public DateTime SubmittedOn { get; set; } = DateTime.Now;

    /// <summary>The mean of the four scales, on the same 1–5 range.</summary>
    [NotMapped]
    public double AverageRating =>
        (OverallRating + ContentRating + VenueRating + OrganizationRating) / 4.0;
}

// ===========================================================================
//  ADMINISTRATION
// ===========================================================================

/// <summary>Trail of account, role, and record changes for the admin module.</summary>
public class AuditLog
{
    public int Id { get; set; }

    public DateTime OccurredOn { get; set; } = DateTime.Now;

    public string? ActorUserId { get; set; }
    public ApplicationUser? Actor { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(40)]
    public string? EntityId { get; set; }

    [StringLength(500)]
    public string? Detail { get; set; }
}
