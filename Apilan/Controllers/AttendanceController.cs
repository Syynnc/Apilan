using System.Text.Json;
using Apilan.Data;
using Apilan.Models;
using Apilan.Services;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Transactions · Check-in. The door screen is the one place in the system
/// that has to work in a queue of people: one field, always focused, that
/// takes a code and answers in one line. Everything else on the screen is
/// there to let an officer recover when the code does not work.
///
/// Every arrival lands as Present or Late depending on the session's grace
/// period, and an officer closing a session can write down who never came, so
/// an absence is a recorded decision rather than a gap in the table.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class AttendanceController : Controller
{
    private const string FeedbackKey = "ScanFeedback";

    private readonly ApilanDbContext _db;
    private readonly RegistrationService _registrations;
    private readonly UserManager<ApplicationUser> _users;

    public AttendanceController(
        ApilanDbContext db,
        RegistrationService registrations,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _registrations = registrations;
        _users = users;
    }

    // ---- Which door? ------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        var options = await _db.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published
                     || e.Status == EventStatus.Ongoing
                     || (e.Status == EventStatus.Completed && e.EndsAt >= today.AddDays(-14)))
            .OrderBy(e => e.StartsAt)
            .Select(e => new AttendanceEventOption
            {
                EventId = e.Id,
                Title = e.Title,
                ColorKey = e.EventType!.ColorKey,
                VenueName = e.Venue!.Name,
                StartsAt = e.StartsAt,
                Status = e.Status,
                Sessions = e.Sessions.Count,
                Expected = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
                Present = e.Registrations.Count(r =>
                    r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent))
            })
            .ToListAsync();

        return View(new AttendancePickerViewModel
        {
            Today = options.Where(o => o.StartsAt.Date == today).ToList(),
            Upcoming = options.Where(o => o.StartsAt.Date > today).ToList(),
            Recent = options.Where(o => o.StartsAt.Date < today).OrderByDescending(o => o.StartsAt).ToList()
        });
    }

    // ---- The door ---------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Door(int id, int? sessionId, string? search)
    {
        var ev = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Organization)
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        if (!ev.Sessions.Any())
        {
            TempData["Error"] = "This event has no sessions, so there is nothing to take attendance against.";
            return RedirectToAction("Edit", "Events", new { id });
        }

        var sessions = await SessionSummaries.ForEventAsync(_db, id);

        // Default to whatever is happening now, else the first session that has
        // not finished, else the last one.
        var session = sessionId.HasValue
            ? sessions.FirstOrDefault(s => s.Id == sessionId.Value)
            : sessions.FirstOrDefault(s => s.IsNow)
              ?? sessions.FirstOrDefault(s => s.EndsAt >= DateTime.Now)
              ?? sessions.LastOrDefault();

        if (session is null)
        {
            return NotFound();
        }

        var rows = await DoorRowsAsync(id, session.Id, search);

        var model = new DoorViewModel
        {
            Event = ev,
            Sessions = sessions,
            Session = session,
            Search = search,
            Feed = rows.Where(r => r.IsPresent).OrderByDescending(r => r.TimeIn).ToList(),
            Expected = rows.Where(r => !r.IsPresent).OrderBy(r => r.ListName).ToList(),
            ExpectedCount = rows.Count(r => r.Status == RegistrationStatus.Confirmed),
            PresentCount = rows.Count(r => r.AttendanceStatus == AttendanceStatus.Present),
            LateCount = rows.Count(r => r.IsLate),
            AbsentCount = rows.Count(r => r.IsAbsent),
            StillInCount = rows.Count(r => r.IsStillIn),
            WalkInCount = rows.Count(r => r.IsWalkIn && r.IsPresent),
            UnaccountedCount = rows.Count(r =>
                r.IsUnaccounted && r.Status != RegistrationStatus.Cancelled),
            Feedback = ReadFeedback()
        };

        return View(model);
    }

    /// <summary>
    /// One code in, one answer out. Scanning someone already inside times them
    /// out instead of erroring — that is what the second scan at the door means.
    ///
    /// A scan that is not a check-in code is tried as a student number before
    /// giving up: campus ID cards carry the student number, so the same field
    /// works whether the person brought their stub or their ID.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(int id, int sessionId, string code, CheckInMethod method = CheckInMethod.ManualSearch)
    {
        var normalized = RegistrationCode.Normalize(code);

        if (string.IsNullOrEmpty(normalized))
        {
            return Back(id, sessionId, ScanFeedback.Bad("Nothing scanned", "Type or scan a check-in code."));
        }

        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Code == normalized && r.EventId == id);

        if (registration is not null)
        {
            return await RecordAsync(id, sessionId, registration, method);
        }

        // Not a stub code — try it as a student number, off the raw input
        // rather than the normalized form, since student numbers carry a dash
        // and their own digits.
        var studentNumber = code.Trim();

        var byStudentNumber = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.EventId == id && r.Member!.StudentNumber == studentNumber);

        if (byStudentNumber is not null)
        {
            return await RecordAsync(id, sessionId, byStudentNumber, CheckInMethod.StudentNumber);
        }

        // A code that belongs to a different event is the most common mistake
        // at a door running back-to-back activities — say so.
        var elsewhere = await _db.Registrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Code == normalized);

        if (elsewhere is not null)
        {
            return Back(id, sessionId, ScanFeedback.Bad("Wrong event",
                $"{normalized} is for \"{elsewhere.Event?.Title}\".", normalized));
        }

        // On the roster of no event at all, but a real person? Say which,
        // because the fix is to add them as a walk-in rather than re-scan.
        var known = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);

        return Back(id, sessionId, known is null
            ? ScanFeedback.Bad("No such code", $"{normalized} does not match any registration.", normalized)
            : ScanFeedback.Bad("Not registered",
                $"{known.FullName} is on the roster but not registered for this event. Add them as a walk-in.",
                studentNumber));
    }

    /// <summary>Manual check-in from the expected list, when a code is lost.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(int id, int sessionId, int registrationId)
    {
        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (registration is null)
        {
            return NotFound();
        }

        return await RecordAsync(id, sessionId, registration, CheckInMethod.ManualSearch);
    }

    /// <summary>
    /// Writes down that somebody did not come. An absence is a statement an
    /// officer makes, so it is a row with their name on it — not the silence
    /// left by never scanning.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAbsent(int id, int sessionId, int registrationId, string? note)
    {
        var session = await _db.EventSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.EventId == id);

        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (session is null || registration is null)
        {
            return NotFound();
        }

        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.RegistrationId == registrationId && a.EventSessionId == sessionId);

        var who = registration.Member?.FullName ?? registration.Code;

        if (existing is not null && existing.Status != AttendanceStatus.Absent)
        {
            return Back(id, sessionId, ScanFeedback.Warn("Already checked in",
                $"{who} is on record as {existing.Status.ToString().ToLowerInvariant()}. "
                + "Undo the check-in first if that was a mistake.", registration.Code));
        }

        if (existing is not null)
        {
            return Back(id, sessionId, ScanFeedback.Warn("Already marked absent",
                $"{who} was already accounted for.", registration.Code));
        }

        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            RegistrationId = registrationId,
            EventSessionId = sessionId,
            Status = AttendanceStatus.Absent,
            TimeIn = null,
            Method = CheckInMethod.ManualSearch,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedByUserId = _users.GetUserId(User)
        });

        AuditTrail.Record(_db, _users.GetUserId(User), "MarkedAbsent", nameof(AttendanceRecord),
            registration.Code, $"{who} absent for \"{session.Name}\". {note}".Trim());

        await _db.SaveChangesAsync();

        return Back(id, sessionId, ScanFeedback.Warn("Marked absent", $"{who} · {session.Name}", registration.Code));
    }

    /// <summary>
    /// Closes the session off: everybody still unaccounted for is marked
    /// absent in one move. Doing this one name at a time for a hall of 300 is
    /// how attendance sheets end up half-finished.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int id, int sessionId)
    {
        var session = await _db.EventSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.EventId == id);

        if (session is null)
        {
            return NotFound();
        }

        // Confirmed registrations with nothing on file for this session. The
        // waitlisted and the cancelled are not expected, so they are not absent.
        var outstanding = await _db.Registrations
            .Where(r => r.EventId == id
                     && r.Status == RegistrationStatus.Confirmed
                     && !r.AttendanceRecords.Any(a => a.EventSessionId == sessionId))
            .Select(r => r.Id)
            .ToListAsync();

        if (!outstanding.Any())
        {
            return Back(id, sessionId, ScanFeedback.Ok("Nothing outstanding",
                $"Everyone expected at \"{session.Name}\" is already accounted for."));
        }

        var actor = _users.GetUserId(User);

        foreach (var registrationId in outstanding)
        {
            _db.AttendanceRecords.Add(new AttendanceRecord
            {
                RegistrationId = registrationId,
                EventSessionId = sessionId,
                Status = AttendanceStatus.Absent,
                TimeIn = null,
                Method = CheckInMethod.ManualSearch,
                Note = "Marked absent when the session was closed.",
                RecordedByUserId = actor
            });
        }

        AuditTrail.Record(_db, actor, "SessionClosed", nameof(EventSession), session.Id,
            $"\"{session.Name}\" closed; {outstanding.Count} registration(s) marked absent.");

        await _db.SaveChangesAsync();

        return Back(id, sessionId, ScanFeedback.Ok("Session closed",
            $"{outstanding.Count} registration(s) marked absent for \"{session.Name}\"."));
    }

    /// <summary>
    /// Changes a record's status by hand — the late arrival who was actually
    /// held up at the gate, or the absence entered against the wrong name.
    /// Overrides are logged with whatever reason the officer gave.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        int id, int sessionId, int attendanceId, AttendanceStatus status, string? note)
    {
        var record = await _db.AttendanceRecords
            .Include(a => a.Registration)!.ThenInclude(r => r!.Member)
            .Include(a => a.EventSession)
            .FirstOrDefaultAsync(a => a.Id == attendanceId && a.EventSession!.EventId == id);

        if (record is null)
        {
            return NotFound();
        }

        var who = record.Registration?.Member?.FullName ?? record.Registration?.Code ?? "Participant";
        var was = record.Status;

        if (was == status)
        {
            return Back(id, sessionId, ScanFeedback.Warn("No change",
                $"{who} is already marked {status.ToString().ToLowerInvariant()}."));
        }

        record.Status = status;
        record.Note = string.IsNullOrWhiteSpace(note) ? record.Note : note.Trim();

        // Going to absent drops the arrival times: there was no arrival.
        if (status == AttendanceStatus.Absent)
        {
            record.TimeIn = null;
            record.TimeOut = null;
        }
        else
        {
            record.TimeIn ??= DateTime.Now;
        }

        AuditTrail.Record(_db, _users.GetUserId(User), "AttendanceStatusChanged", nameof(AttendanceRecord),
            record.Id, $"{who} on \"{record.EventSession?.Name}\": {was} → {status}. {note}".Trim());

        await _db.SaveChangesAsync();

        return Back(id, sessionId, ScanFeedback.Ok($"Marked {status.ToString().ToLowerInvariant()}",
            $"{who} · was {was.ToString().ToLowerInvariant()}", record.Registration?.Code));
    }

    /// <summary>Times someone out without needing their stub a second time.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TimeOut(int id, int sessionId, int attendanceId)
    {
        var record = await _db.AttendanceRecords
            .Include(a => a.Registration)!.ThenInclude(r => r!.Member)
            .FirstOrDefaultAsync(a => a.Id == attendanceId);

        if (record is null)
        {
            return NotFound();
        }

        var who = record.Registration?.Member?.FullName;

        if (record.Status == AttendanceStatus.Absent)
        {
            return Back(id, sessionId, ScanFeedback.Bad("Marked absent",
                $"{who} is on record as absent, so there is nothing to time out."));
        }

        if (record.TimeOut.HasValue)
        {
            return Back(id, sessionId, ScanFeedback.Warn("Already timed out",
                $"{who} left at {record.TimeOut:h:mm tt}."));
        }

        record.TimeOut = DateTime.Now;
        await _db.SaveChangesAsync();

        return Back(id, sessionId, ScanFeedback.Ok("Timed out",
            $"{who} at {record.TimeOut:h:mm tt}.",
            record.Registration?.Code));
    }

    /// <summary>
    /// Undoes a mis-scan. Attendance is evidence, so removing a record is
    /// logged rather than silent.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int id, int sessionId, int attendanceId)
    {
        var record = await _db.AttendanceRecords
            .Include(a => a.Registration)!.ThenInclude(r => r!.Member)
            .FirstOrDefaultAsync(a => a.Id == attendanceId);

        if (record is null)
        {
            return NotFound();
        }

        var who = record.Registration?.Member?.FullName;
        var was = record.Status;

        _db.AttendanceRecords.Remove(record);

        AuditTrail.Record(_db, _users.GetUserId(User), "AttendanceRemoved", nameof(AttendanceRecord), record.Id,
            $"{who} on session {sessionId}, was {was}.");
        await _db.SaveChangesAsync();

        return Back(id, sessionId, ScanFeedback.Warn("Record removed",
            $"{who} is back on the expected list."));
    }

    /// <summary>
    /// Registers a walk-in and checks them in, in one move — the person is at
    /// the table and the queue is not getting shorter.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WalkIn(int id, int sessionId, int memberId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
        {
            return NotFound();
        }

        var result = await _registrations.RegisterAsync(ev, memberId, isWalkIn: true, _users.GetUserId(User));

        if (!result.Succeeded || result.Registration is null)
        {
            return Back(id, sessionId, ScanFeedback.Bad("Could not add", result.Message));
        }

        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstAsync(r => r.Id == result.Registration.Id);

        return await RecordAsync(id, sessionId, registration, CheckInMethod.ManualSearch, walkIn: true);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<IActionResult> RecordAsync(
        int eventId, int sessionId, Registration registration, CheckInMethod method, bool walkIn = false)
    {
        var session = await _db.EventSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.EventId == eventId);

        if (session is null)
        {
            return NotFound();
        }

        var who = registration.Member?.FullName ?? registration.Code;
        var line = $"{registration.Member?.StudentNumber} · {session.Name}";

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return Back(eventId, sessionId, ScanFeedback.Bad("Cancelled stub",
                $"{who}'s registration was cancelled.", registration.Code));
        }

        if (registration.Status == RegistrationStatus.Waitlisted)
        {
            return Back(eventId, sessionId, ScanFeedback.Warn("On the waitlist",
                $"{who} is number {registration.WaitlistPosition} in the queue. Promote them first.", registration.Code));
        }

        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.RegistrationId == registration.Id && a.EventSessionId == sessionId);

        // Someone marked absent who then walks in is not an error — it is the
        // common case of a latecomer. Turn the absence into the arrival.
        if (existing is not null && existing.Status == AttendanceStatus.Absent)
        {
            var arrived = DateTime.Now;

            existing.Status = AttendanceRules.Classify(arrived, session.StartsAt, session.LateAfterMinutes);
            existing.TimeIn = arrived;
            existing.Method = method;
            existing.Note = "Marked absent, then turned up.";
            existing.RecordedByUserId = _users.GetUserId(User);

            AuditTrail.Record(_db, _users.GetUserId(User), "AttendanceStatusChanged", nameof(AttendanceRecord),
                existing.Id, $"{who} on \"{session.Name}\": Absent → {existing.Status} on arrival.");

            await _db.SaveChangesAsync();

            return Back(eventId, sessionId, ScanFeedback.Ok(
                existing.Status == AttendanceStatus.Late ? $"Late · {who}" : who,
                $"{line} · was marked absent, in at {arrived:h:mm tt}", registration.Code));
        }

        if (existing is not null)
        {
            if (existing.TimeOut.HasValue)
            {
                return Back(eventId, sessionId, ScanFeedback.Warn("Already recorded",
                    $"{who} was in at {existing.TimeIn:h:mm tt} and out at {existing.TimeOut:h:mm tt}.", registration.Code));
            }

            existing.TimeOut = DateTime.Now;
            await _db.SaveChangesAsync();

            return Back(eventId, sessionId, ScanFeedback.Ok("Timed out",
                $"{who} at {existing.TimeOut:h:mm tt}.", registration.Code));
        }

        // A pending registration that turns up at the door is confirmed by the
        // fact of turning up; the officer scanning them is the approval.
        if (registration.Status == RegistrationStatus.Pending)
        {
            registration.Status = RegistrationStatus.Confirmed;
        }

        var now = DateTime.Now;
        var status = AttendanceRules.Classify(now, session.StartsAt, session.LateAfterMinutes);

        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            RegistrationId = registration.Id,
            EventSessionId = sessionId,
            Status = status,
            TimeIn = now,
            Method = method,
            RecordedByUserId = _users.GetUserId(User)
        });

        await _db.SaveChangesAsync();

        var headline = walkIn
            ? $"Walk-in · {who}"
            : status == AttendanceStatus.Late ? $"Late · {who}" : who;

        var detail = status == AttendanceStatus.Late
            ? $"{line} · in at {now:h:mm tt}, {Minutes(now - session.LateThreshold)} past the grace period"
            : $"{line} · in at {now:h:mm tt}";

        return Back(eventId, sessionId, status == AttendanceStatus.Late
            ? ScanFeedback.Warn(headline, detail, registration.Code)
            : ScanFeedback.Ok(headline, detail, registration.Code));
    }

    /// <summary>"7 min" / "1 hr 20 min" — how late, in words an officer can read at speed.</summary>
    private static string Minutes(TimeSpan late)
    {
        var total = (int)Math.Round(late.TotalMinutes);

        if (total < 60)
        {
            return $"{Math.Max(1, total)} min";
        }

        return $"{total / 60} hr {total % 60} min";
    }

    private async Task<List<DoorRow>> DoorRowsAsync(int eventId, int sessionId, string? search)
    {
        var query = _db.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId && r.Status != RegistrationStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Member!.LastName.Contains(term) ||
                r.Member.FirstName.Contains(term) ||
                r.Member.StudentNumber.Contains(term) ||
                r.Code.Contains(term));
        }

        return await query
            .Select(r => new DoorRow
            {
                RegistrationId = r.Id,
                Code = r.Code,
                ListName = r.Member!.LastName + ", " + r.Member.FirstName,
                StudentNumber = r.Member.StudentNumber,
                CourseYear = r.Member.Course + " · " + r.Member.YearLevel,
                Status = r.Status,
                IsWalkIn = r.IsWalkIn,
                AttendanceId = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => (int?)a.Id)
                    .FirstOrDefault(),
                AttendanceStatus = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => (AttendanceStatus?)a.Status)
                    .FirstOrDefault(),
                TimeIn = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => a.TimeIn)
                    .FirstOrDefault(),
                TimeOut = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => a.TimeOut)
                    .FirstOrDefault(),
                Method = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => (CheckInMethod?)a.Method)
                    .FirstOrDefault(),
                Note = r.AttendanceRecords
                    .Where(a => a.EventSessionId == sessionId)
                    .Select(a => a.Note)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    /// <summary>
    /// Back to the door with the scan result. Redirecting rather than
    /// rendering keeps the field empty and refresh-safe between people.
    /// </summary>
    private IActionResult Back(int eventId, int sessionId, ScanFeedback feedback)
    {
        TempData[FeedbackKey] = JsonSerializer.Serialize(feedback);
        return RedirectToAction(nameof(Door), new { id = eventId, sessionId });
    }

    private ScanFeedback? ReadFeedback() =>
        TempData[FeedbackKey] is string json
            ? JsonSerializer.Deserialize<ScanFeedback>(json)
            : null;
}
