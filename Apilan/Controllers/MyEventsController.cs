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
/// The member's own area: the stubs they hold, what they attended, and what
/// they earned. Everything here is scoped to the signed-in member's record —
/// a member never sees another member's registrations.
/// </summary>
[Authorize]
public class MyEventsController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly RegistrationService _registrations;
    private readonly UserManager<ApplicationUser> _users;

    public MyEventsController(
        ApilanDbContext db,
        RegistrationService registrations,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _registrations = registrations;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);

        var model = new MyEventsViewModel
        {
            FullName = user?.FullName ?? "there",
            HasMemberRecord = user?.MemberId is not null
        };

        if (user?.MemberId is null)
        {
            return View(model);
        }

        var registrations = await _db.Registrations
            .AsNoTracking()
            .Include(r => r.Member)
            .Include(r => r.Feedback)
            .Include(r => r.Event)!.ThenInclude(e => e!.EventType)
            .Include(r => r.Event)!.ThenInclude(e => e!.Venue)
            .Include(r => r.Event)!.ThenInclude(e => e!.Organization)
            .Include(r => r.Event)!.ThenInclude(e => e!.Sessions)
            .Include(r => r.AttendanceRecords)
            .Where(r => r.MemberId == user.MemberId)
            .OrderBy(r => r.Event!.StartsAt)
            .ToListAsync();

        foreach (var registration in registrations)
        {
            var ev = registration.Event!;

            // An absence is a record too, so "attended" is the records that
            // are not absences rather than all of them.
            var attended = registration.AttendanceRecords
                .Count(a => AttendanceRules.CountsAsAttended(a.Status));

            var row = new MyEventRow
            {
                RegistrationId = registration.Id,
                EventId = ev.Id,
                StartsAt = ev.StartsAt,
                EndsAt = ev.EndsAt,
                Status = registration.Status,
                Attended = attended,
                Late = registration.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Late),
                Sessions = ev.Sessions.Count,
                Eligibility = registration.Eligibility,
                Earned = AttendanceRules.IsEarned(registration.Eligibility),
                CollectsFeedback = ev.CollectsFeedback,
                HasFeedback = registration.Feedback is not null,
                FeedbackOn = registration.Feedback?.SubmittedOn,
                Ticket = TicketViewModel.FromRegistration(
                    registration,
                    ev.Organization?.Name ?? "Apilan",
                    checkedIn: attended > 0)
            };

            if (ev.EndsAt >= DateTime.Now && registration.Status != RegistrationStatus.Cancelled)
            {
                model.Upcoming.Add(row);
            }
            else
            {
                model.Past.Add(row);
            }
        }

        model.Past.Reverse();
        return View(model);
    }

    /// <summary>
    /// The stub's QR symbol as a PNG, for saving to a phone's photos or
    /// printing. The stub itself renders the same symbol inline; this is for
    /// when a member wants the code somewhere their screen lock cannot hide it.
    ///
    /// Scoped to the member's own registration: a check-in code is a bearer
    /// token at the door, so nobody gets to fetch somebody else's.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Qr(int id)
    {
        var user = await _users.GetUserAsync(User);

        if (user?.MemberId is null)
        {
            return NotFound();
        }

        var registration = await _db.Registrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.MemberId == user.MemberId);

        if (registration is null)
        {
            return NotFound();
        }

        var png = Helpers.QrCode.Png(registration.Code, pixelsPerModule: 12);

        return File(png, "image/png", $"apilan-{registration.Code}.png");
    }

    /// <summary>
    /// A member dropping out before the event starts. The freed slot goes to
    /// the head of the waitlist inside <see cref="RegistrationService"/>.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _users.GetUserAsync(User);

        var registration = await _db.Registrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id && r.MemberId == user!.MemberId);

        if (registration is null)
        {
            return NotFound();
        }

        if (registration.Event!.StartsAt <= DateTime.Now)
        {
            TempData["Error"] = "That event has already started, so the registration can no longer be dropped.";
            return RedirectToAction(nameof(Index));
        }

        await _registrations.CancelAsync(registration, user!.Id, "Dropped by the member.");

        TempData["Success"] = $"You are no longer registered for \"{registration.Event.Title}\".";
        return RedirectToAction(nameof(Index));
    }
}
