using Apilan.Models;

namespace Apilan.Services;

/// <summary>
/// The one place that turns attendance records into a certificate decision.
/// Both the Certificates screen and the participation report read from here,
/// so an officer and a report can never disagree about who earned what.
/// </summary>
public static class AttendanceRules
{
    /// <summary>
    /// Whether a record counts as having turned up. Present and late both do —
    /// arriving late is a punctuality problem, not an absence, and an org that
    /// treated it as one would be refusing certificates to people who sat
    /// through the whole thing.
    /// </summary>
    public static bool CountsAsAttended(AttendanceStatus status) =>
        status != AttendanceStatus.Absent;

    /// <summary>
    /// What an arrival at <paramref name="timeIn"/> means for a session that
    /// started at <paramref name="startsAt"/> with a grace period of
    /// <paramref name="lateAfterMinutes"/>. Arriving early is on time.
    /// </summary>
    public static AttendanceStatus Classify(DateTime timeIn, DateTime startsAt, int lateAfterMinutes) =>
        timeIn > startsAt.AddMinutes(lateAfterMinutes)
            ? AttendanceStatus.Late
            : AttendanceStatus.Present;

    /// <summary>Chip class for an attendance status.</summary>
    public static string StatusChip(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "ok",
        AttendanceStatus.Late => "warn",
        _ => "risk"
    };

    /// <summary>
    /// Share of an event's sessions the registration was present for, 0–100.
    /// An event with no sessions yet counts as 0 rather than dividing by zero.
    /// </summary>
    public static int AttendancePercent(int attended, int totalSessions) =>
        totalSessions <= 0 ? 0 : (int)Math.Round(attended * 100.0 / totalSessions, MidpointRounding.AwayFromZero);

    /// <summary>
    /// What the rule says, ignoring officer overrides. Only confirmed
    /// participants can earn one — a cancelled stub never does.
    /// </summary>
    public static bool MeetsThreshold(RegistrationStatus status, int attended, int totalSessions, int thresholdPercent)
        => status == RegistrationStatus.Confirmed
           && totalSessions > 0
           && AttendancePercent(attended, totalSessions) >= thresholdPercent;

    /// <summary>
    /// The stored eligibility for a registration, respecting an officer's
    /// override. Overrides are deliberate decisions, so recomputing never
    /// silently undoes one.
    /// </summary>
    public static EligibilityStatus Resolve(
        EligibilityStatus current,
        RegistrationStatus status,
        int attended,
        int totalSessions,
        int thresholdPercent)
    {
        if (current is EligibilityStatus.OverrideGranted or EligibilityStatus.OverrideRevoked)
        {
            return current;
        }

        return MeetsThreshold(status, attended, totalSessions, thresholdPercent)
            ? EligibilityStatus.Eligible
            : EligibilityStatus.NotEligible;
    }

    /// <summary>Whether a certificate may actually be issued.</summary>
    public static bool IsEarned(EligibilityStatus eligibility) =>
        eligibility is EligibilityStatus.Eligible or EligibilityStatus.OverrideGranted;

    /// <summary>Chip class for the design system.</summary>
    public static string Chip(EligibilityStatus eligibility) => eligibility switch
    {
        EligibilityStatus.Eligible => "ok",
        EligibilityStatus.OverrideGranted => "info",
        EligibilityStatus.OverrideRevoked => "risk",
        _ => "idle"
    };

    /// <summary>Label for the design system.</summary>
    public static string Label(EligibilityStatus eligibility) => eligibility switch
    {
        EligibilityStatus.Eligible => "Eligible",
        EligibilityStatus.OverrideGranted => "Granted",
        EligibilityStatus.OverrideRevoked => "Revoked",
        _ => "Not eligible"
    };
}
