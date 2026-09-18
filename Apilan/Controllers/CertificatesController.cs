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
/// Transactions · Certificates. Eligibility is computed from attendance, not
/// typed in; an officer can still override a single decision, and an override
/// survives every later recompute because it was a deliberate judgement.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class CertificatesController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public CertificatesController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = await _db.Events
            .AsNoTracking()
            .Where(e => e.IssuesCertificate && e.Status != EventStatus.Draft && e.Status != EventStatus.Cancelled)
            .OrderByDescending(e => e.StartsAt)
            .Select(e => new CertificateEventRow
            {
                EventId = e.Id,
                Title = e.Title,
                ColorKey = e.EventType!.ColorKey,
                TypeLabel = e.EventType.Name,
                StartsAt = e.StartsAt,
                Status = e.Status,
                ThresholdPercent = e.CertificateThresholdPercent,
                Sessions = e.Sessions.Count,
                Participants = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
                Earned = e.Registrations.Count(r =>
                    r.Eligibility == EligibilityStatus.Eligible ||
                    r.Eligibility == EligibilityStatus.OverrideGranted)
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Event(int id, string? filter)
    {
        var ev = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Sessions)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        var sessionCount = ev.Sessions.Count;

        var rows = await _db.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == id && r.Status != RegistrationStatus.Cancelled)
            .OrderBy(r => r.Member!.LastName).ThenBy(r => r.Member!.FirstName)
            .Select(r => new CertificateRow
            {
                RegistrationId = r.Id,
                MemberId = r.MemberId,
                Code = r.Code,
                ListName = r.Member!.LastName + ", " + r.Member.FirstName,
                FullName = r.Member.FirstName + " " + r.Member.LastName,
                StudentNumber = r.Member.StudentNumber,
                CourseYear = r.Member.Course + " · " + r.Member.YearLevel,
                Status = r.Status,
                Attended = r.AttendanceRecords.Count(a => a.Status != AttendanceStatus.Absent),
                Sessions = sessionCount,
                Eligibility = r.Eligibility,
                Note = r.EligibilityNote
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            row.MeetsRule = AttendanceRules.MeetsThreshold(
                row.Status, row.Attended, sessionCount, ev.CertificateThresholdPercent);
        }

        var filtered = filter switch
        {
            "earned" => rows.Where(r => AttendanceRules.IsEarned(r.Eligibility)).ToList(),
            "short" => rows.Where(r => !AttendanceRules.IsEarned(r.Eligibility)).ToList(),
            "overridden" => rows.Where(r => r.IsOverride).ToList(),
            _ => rows
        };

        return View(new CertificateListViewModel
        {
            Event = ev,
            Rows = filtered,
            Filter = filter,
            Total = rows.Count,
            Earned = rows.Count(r => AttendanceRules.IsEarned(r.Eligibility)),
            Overridden = rows.Count(r => r.IsOverride),
            Unsettled = rows.Count(r => r.MeetsRule != AttendanceRules.IsEarned(r.Eligibility) && !r.IsOverride)
        });
    }

    /// <summary>
    /// Re-runs the rule over every participant. Useful after a late
    /// attendance correction, or after the threshold itself is changed.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recompute(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Sessions)
            .Include(e => e.Registrations)!.ThenInclude(r => r.AttendanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        var settled = EventsController.SettleEligibility(ev);

        AuditTrail.Record(_db, _users.GetUserId(User), "EligibilityRecomputed", nameof(Event), ev.Id,
            $"{ev.Title}: {settled} participant(s) re-checked.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Re-checked {settled} participant(s) against the {ev.CertificateThresholdPercent}% rule.";
        return RedirectToAction(nameof(Event), new { id });
    }

    /// <summary>
    /// An officer's judgement over the rule — granted when someone was there
    /// but the record is short, revoked when the record flatters them.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Override(int id, int registrationId, bool grant, string? note)
    {
        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (registration is null)
        {
            return NotFound();
        }

        registration.Eligibility = grant ? EligibilityStatus.OverrideGranted : EligibilityStatus.OverrideRevoked;
        registration.EligibilityNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        AuditTrail.Record(_db, _users.GetUserId(User),
            grant ? "CertificateGranted" : "CertificateRevoked",
            nameof(Registration), registration.Code,
            $"{registration.Member?.ListName}. {registration.EligibilityNote}".Trim());
        await _db.SaveChangesAsync();

        TempData["Success"] = grant
            ? $"{registration.Member?.FullName} was granted a certificate."
            : $"{registration.Member?.FullName}'s certificate was revoked.";

        return RedirectToAction(nameof(Event), new { id });
    }

    /// <summary>Drops an override so the rule decides again.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOverride(int id, int registrationId)
    {
        var ev = await _db.Events
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id);

        var registration = await _db.Registrations
            .Include(r => r.Member)
            .Include(r => r.AttendanceRecords)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (ev is null || registration is null)
        {
            return NotFound();
        }

        registration.EligibilityNote = null;
        registration.Eligibility = AttendanceRules.MeetsThreshold(
            registration.Status,
            registration.AttendanceRecords.Count(a => AttendanceRules.CountsAsAttended(a.Status)),
            ev.Sessions.Count,
            ev.CertificateThresholdPercent)
                ? EligibilityStatus.Eligible
                : EligibilityStatus.NotEligible;

        AuditTrail.Record(_db, _users.GetUserId(User), "CertificateOverrideCleared", nameof(Registration),
            registration.Code, registration.Member?.ListName);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"The override on {registration.Member?.FullName} was cleared.";
        return RedirectToAction(nameof(Event), new { id });
    }
}
