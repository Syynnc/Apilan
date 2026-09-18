using Apilan.Data;
using Apilan.Models;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Services;

/// <summary>What happened when someone tried to take a slot.</summary>
public enum RegistrationOutcome
{
    /// <summary>A slot was taken and the stub is live.</summary>
    Confirmed,
    /// <summary>The event asks for officer approval, so the stub is not live yet.</summary>
    PendingApproval,
    /// <summary>Capacity was reached, so the person is in the queue.</summary>
    Waitlisted,
    /// <summary>The member already has a stub for this event.</summary>
    AlreadyRegistered,
    /// <summary>Registration is closed: not published, cancelled, or past the deadline.</summary>
    Closed,
    /// <summary>Full, and the event does not keep a waitlist.</summary>
    Full
}

public record RegistrationResult(RegistrationOutcome Outcome, Registration? Registration)
{
    public bool Succeeded => Outcome is RegistrationOutcome.Confirmed
        or RegistrationOutcome.PendingApproval
        or RegistrationOutcome.Waitlisted;

    /// <summary>A sentence the screen can show as-is.</summary>
    public string Message => Outcome switch
    {
        RegistrationOutcome.Confirmed => "Registered. The stub is ready to present at the door.",
        RegistrationOutcome.PendingApproval => "Registered. An officer still has to approve the slot.",
        RegistrationOutcome.Waitlisted => "The event is full, so the registration is on the waitlist.",
        RegistrationOutcome.AlreadyRegistered => "That member already has a registration for this event.",
        RegistrationOutcome.Closed => "Registration for this event is closed.",
        RegistrationOutcome.Full => "The event is full and does not keep a waitlist.",
        _ => string.Empty
    };
}

/// <summary>
/// Everything that has to stay true about slots: capacity, the waitlist queue,
/// and the unique check-in code. Both the officer console and the public
/// calendar go through here, so the rules cannot drift apart.
/// </summary>
public class RegistrationService
{
    private readonly ApilanDbContext _db;

    public RegistrationService(ApilanDbContext db) => _db = db;

    /// <summary>Slots actually taken — confirmed and pending both hold one.</summary>
    public Task<int> TakenSlotsAsync(int eventId) =>
        _db.Registrations.CountAsync(r =>
            r.EventId == eventId &&
            (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending));

    /// <summary>
    /// Registers a member. Walk-ins are recorded by an officer at the door, so
    /// they skip the approval step and the deadline — the person is standing
    /// right there.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(
        Event ev, int memberId, bool isWalkIn, string? actorUserId, string? remarks = null)
    {
        var existing = await _db.Registrations
            .FirstOrDefaultAsync(r => r.EventId == ev.Id && r.MemberId == memberId);

        if (existing is not null)
        {
            // A cancelled stub can be revived rather than leaving the member stuck:
            // the unique index means there is only ever one row per member per event.
            if (existing.Status != RegistrationStatus.Cancelled)
            {
                return new RegistrationResult(RegistrationOutcome.AlreadyRegistered, existing);
            }

            _db.Registrations.Remove(existing);
            await _db.SaveChangesAsync();
        }

        if (!isWalkIn && !IsOpenForRegistration(ev))
        {
            return new RegistrationResult(RegistrationOutcome.Closed, null);
        }

        if (isWalkIn && ev.Status is EventStatus.Draft or EventStatus.Cancelled)
        {
            return new RegistrationResult(RegistrationOutcome.Closed, null);
        }

        var taken = await TakenSlotsAsync(ev.Id);
        var isFull = taken >= ev.Capacity;

        RegistrationStatus status;
        int? waitlistPosition = null;

        if (isFull && !isWalkIn)
        {
            if (!ev.AllowWaitlist)
            {
                return new RegistrationResult(RegistrationOutcome.Full, null);
            }

            status = RegistrationStatus.Waitlisted;
            waitlistPosition = await NextWaitlistPositionAsync(ev.Id);
        }
        else
        {
            // Walk-ins are let in even over capacity — the officer decided at
            // the door, and the report shows the overflow honestly.
            status = ev.RequiresApproval && !isWalkIn
                ? RegistrationStatus.Pending
                : RegistrationStatus.Confirmed;
        }

        var registration = new Registration
        {
            EventId = ev.Id,
            MemberId = memberId,
            Code = await NextCodeAsync(),
            Status = status,
            WaitlistPosition = waitlistPosition,
            IsWalkIn = isWalkIn,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            RegisteredOn = DateTime.Now
        };

        _db.Registrations.Add(registration);

        AuditTrail.Record(_db, actorUserId,
            isWalkIn ? "WalkInRegistered" : "Registered",
            nameof(Registration), registration.Code,
            $"Member {memberId} on \"{ev.Title}\" as {status}.");

        await _db.SaveChangesAsync();

        var outcome = status switch
        {
            RegistrationStatus.Waitlisted => RegistrationOutcome.Waitlisted,
            RegistrationStatus.Pending => RegistrationOutcome.PendingApproval,
            _ => RegistrationOutcome.Confirmed
        };

        return new RegistrationResult(outcome, registration);
    }

    /// <summary>
    /// Releases a slot. The freed slot goes to whoever has waited longest,
    /// which is the only fair reading of a queue.
    /// </summary>
    public async Task<Registration?> CancelAsync(Registration registration, string? actorUserId, string? reason = null)
    {
        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return null;
        }

        var heldASlot = registration.Status is RegistrationStatus.Confirmed or RegistrationStatus.Pending;

        registration.Status = RegistrationStatus.Cancelled;
        registration.WaitlistPosition = null;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            registration.Remarks = reason.Trim();
        }

        AuditTrail.Record(_db, actorUserId, "RegistrationCancelled", nameof(Registration), registration.Code, reason);

        Registration? promoted = null;
        if (heldASlot)
        {
            promoted = await PromoteNextAsync(registration.EventId, actorUserId);
        }

        await _db.SaveChangesAsync();
        return promoted;
    }

    /// <summary>
    /// Moves the head of the queue into the free slot. Returns null when the
    /// queue is empty or the event has no room.
    /// </summary>
    public async Task<Registration?> PromoteNextAsync(int eventId, string? actorUserId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null)
        {
            return null;
        }

        var taken = await TakenSlotsAsync(eventId);
        if (taken >= ev.Capacity)
        {
            return null;
        }

        var next = await _db.Registrations
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.WaitlistPosition ?? int.MaxValue)
            .ThenBy(r => r.RegisteredOn)
            .FirstOrDefaultAsync();

        if (next is null)
        {
            return null;
        }

        next.Status = ev.RequiresApproval ? RegistrationStatus.Pending : RegistrationStatus.Confirmed;
        next.WaitlistPosition = null;

        AuditTrail.Record(_db, actorUserId, "WaitlistPromoted", nameof(Registration), next.Code,
            $"Promoted to {next.Status} on \"{ev.Title}\".");

        await ResequenceWaitlistAsync(eventId);
        return next;
    }

    /// <summary>Keeps queue positions contiguous after a promotion or a cancellation.</summary>
    public async Task ResequenceWaitlistAsync(int eventId)
    {
        var queue = await _db.Registrations
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.WaitlistPosition ?? int.MaxValue)
            .ThenBy(r => r.RegisteredOn)
            .ToListAsync();

        for (var i = 0; i < queue.Count; i++)
        {
            queue[i].WaitlistPosition = i + 1;
        }
    }

    /// <summary>
    /// Whether the public calendar should offer a register button: published,
    /// still ahead of its deadline, and not already over.
    /// </summary>
    public static bool IsOpenForRegistration(Event ev) =>
        ev.Status is EventStatus.Published or EventStatus.Ongoing
        && ev.RegistrationDeadline >= DateTime.Now
        && ev.EndsAt >= DateTime.Now;

    private async Task<int> NextWaitlistPositionAsync(int eventId)
    {
        var highest = await _db.Registrations
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Waitlisted)
            .MaxAsync(r => (int?)r.WaitlistPosition);

        return (highest ?? 0) + 1;
    }

    /// <summary>
    /// A code short enough to type at the door is short enough to collide, so
    /// it is checked against the table rather than trusted.
    /// </summary>
    private async Task<string> NextCodeAsync()
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var code = RegistrationCode.Next();
            if (!await _db.Registrations.AnyAsync(r => r.Code == code))
            {
                return code;
            }
        }

        // Astronomically unlikely; better a longer code than a failed check-in.
        return RegistrationCode.Next() + Random.Shared.Next(10, 99);
    }
}
