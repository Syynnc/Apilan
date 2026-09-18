namespace Apilan.Models;

/// <summary>
/// Role names. Kept as constants so controllers and seeding agree.
///
/// Two roles: an officer runs the organization, a member attends its events.
/// There is deliberately no separate administrator — an officer can do
/// everything, including managing other accounts and reading the activity log.
/// The trade is that any officer can demote another, so the screens that change
/// roles refuse to leave the system with no officer at all.
/// </summary>
public static class Roles
{
    public const string Officer = "Officer";
    public const string Member = "Member";

    public static readonly string[] All = { Officer, Member };

    /// <summary>Anyone who can run events. Used all over the officer console.</summary>
    public const string Staff = Officer;
}

/// <summary>
/// Where an account request stands. Students self-register and sit at
/// <see cref="Pending"/> until an officer approves them.
/// </summary>
public enum AccountStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2,
    Disabled = 3
}

/// <summary>Whether a member record is still part of the organization.</summary>
public enum MembershipStatus
{
    Active = 0,
    Inactive = 1
}

/// <summary>How a member is affiliated with the organization.</summary>
public enum MembershipType
{
    Regular = 0,
    Associate = 1,
    Officer = 2,
    Alumni = 3
}

/// <summary>Lifecycle of an event, from draft through to close-out.</summary>
public enum EventStatus
{
    Draft = 0,
    Published = 1,
    Ongoing = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// Where a registration stands. Officers move people between these; the system
/// sets <see cref="Waitlisted"/> automatically once capacity is reached.
/// </summary>
public enum RegistrationStatus
{
    Pending = 0,
    Confirmed = 1,
    Waitlisted = 2,
    Cancelled = 3
}

/// <summary>How a participant was recorded at the door. Kept for the audit trail.</summary>
public enum CheckInMethod
{
    ManualSearch = 0,
    QrCode = 1,
    StudentNumber = 2
}

/// <summary>
/// How a participant turned up for one session. <see cref="Absent"/> is a
/// record too: an officer closing a session writes one for everybody who never
/// arrived, so "did not come" is a stated fact rather than a missing row.
/// Only <see cref="Present"/> and <see cref="Late"/> count toward a certificate.
/// </summary>
public enum AttendanceStatus
{
    Present = 0,
    Late = 1,
    Absent = 2
}

/// <summary>Whether a participant has earned a certificate for an event.</summary>
public enum EligibilityStatus
{
    NotEligible = 0,
    Eligible = 1,
    /// <summary>An officer granted it despite the attendance rule not being met.</summary>
    OverrideGranted = 2,
    /// <summary>An officer revoked it despite the attendance rule being met.</summary>
    OverrideRevoked = 3
}
