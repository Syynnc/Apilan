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
/// Event feedback, from both ends: a participant says how it went, and the
/// officers who ran it read the result.
///
/// Feedback is tied to a registration rather than to a member and an event,
/// which gets two rules for free — a person can only rate something they
/// signed up for, and they can only do it once.
/// </summary>
[Authorize]
public class FeedbackController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public FeedbackController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // =======================================================================
    //  THE PARTICIPANT'S SIDE
    // =======================================================================

    /// <summary>
    /// The form for one of the member's own registrations. Revisiting it after
    /// submitting shows what they said, so they can correct it.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Form(int id)
    {
        var registration = await MyRegistrationAsync(id);

        if (registration is null)
        {
            return NotFound();
        }

        var gate = Gate(registration);
        if (gate is not null)
        {
            TempData["Error"] = gate;
            return RedirectToAction("Index", "MyEvents");
        }

        return View(await ShellAsync(registration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, FeedbackFormViewModel model)
    {
        var registration = await MyRegistrationAsync(id);

        if (registration is null)
        {
            return NotFound();
        }

        var gate = Gate(registration);
        if (gate is not null)
        {
            TempData["Error"] = gate;
            return RedirectToAction("Index", "MyEvents");
        }

        if (!ModelState.IsValid)
        {
            // Rebuild the event context; only the answers came from the post.
            var shell = await ShellAsync(registration);

            shell.OverallRating = model.OverallRating;
            shell.ContentRating = model.ContentRating;
            shell.VenueRating = model.VenueRating;
            shell.OrganizationRating = model.OrganizationRating;
            shell.WouldRecommend = model.WouldRecommend;
            shell.Comment = model.Comment;
            shell.Improvement = model.Improvement;
            shell.IsAnonymous = model.IsAnonymous;

            return View("Form", shell);
        }

        var existing = await _db.EventFeedback
            .FirstOrDefaultAsync(f => f.RegistrationId == registration.Id);

        var isNew = existing is null;

        if (existing is null)
        {
            existing = new EventFeedback { RegistrationId = registration.Id };
            _db.EventFeedback.Add(existing);
        }

        model.ApplyTo(existing);

        AuditTrail.Record(_db, _users.GetUserId(User),
            isNew ? "FeedbackSubmitted" : "FeedbackRevised",
            nameof(EventFeedback), registration.Code,
            $"\"{registration.Event?.Title}\" rated {model.OverallRating}/5 overall.");

        await _db.SaveChangesAsync();

        TempData["Success"] = isNew
            ? "Thanks — your feedback went to the officers who ran it."
            : "Your feedback was updated.";

        return RedirectToAction("Index", "MyEvents");
    }

    // =======================================================================
    //  THE OFFICER'S SIDE
    // =======================================================================

    /// <summary>Every event with responses on file, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Index(int? organizationId)
    {
        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.Registrations.Any(r => r.Feedback != null));

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId.Value);
        }

        var rows = await query
            .OrderByDescending(e => e.StartsAt)
            .Select(e => new FeedbackEventRow
            {
                EventId = e.Id,
                Title = e.Title,
                OrgAcronym = e.Organization!.Acronym,
                TypeLabel = e.EventType!.Name,
                ColorKey = e.EventType.ColorKey,
                StartsAt = e.StartsAt,
                Status = e.Status,
                Attended = e.Registrations.Count(r =>
                    r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent)),
                Responses = e.Registrations.Count(r => r.Feedback != null),
                RecommendCount = e.Registrations.Count(r =>
                    r.Feedback != null && r.Feedback.WouldRecommend),
                AverageRating = e.Registrations
                    .Where(r => r.Feedback != null)
                    .Average(r => (double?)(
                        r.Feedback!.OverallRating +
                        r.Feedback.ContentRating +
                        r.Feedback.VenueRating +
                        r.Feedback.OrganizationRating) / 4.0)
            })
            .ToListAsync();

        return View(new FeedbackListViewModel
        {
            Events = rows,
            OrganizationId = organizationId,
            Organizations = await _db.Organizations
                .AsNoTracking()
                .OrderBy(o => o.Name)
                .Select(o => new OrganizationOption(o.Id, o.Name, o.Acronym, o.ColorKey))
                .ToListAsync()
        });
    }

    /// <summary>
    /// One event's responses. Anonymous submissions keep their scores and
    /// their words — only the name is withheld, because the point of the
    /// option is candour, not exclusion.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Event(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Organization)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        var responses = await _db.EventFeedback
            .AsNoTracking()
            .Where(f => f.Registration!.EventId == id)
            .OrderByDescending(f => f.SubmittedOn)
            .Select(f => new FeedbackResponseRow
            {
                Id = f.Id,
                Who = f.IsAnonymous
                    ? "Anonymous"
                    : f.Registration!.Member!.LastName + ", " + f.Registration.Member.FirstName,
                CourseYear = f.IsAnonymous
                    ? null
                    : f.Registration!.Member!.Course + " · " + f.Registration.Member.YearLevel,
                OverallRating = f.OverallRating,
                ContentRating = f.ContentRating,
                VenueRating = f.VenueRating,
                OrganizationRating = f.OrganizationRating,
                WouldRecommend = f.WouldRecommend,
                Comment = f.Comment,
                Improvement = f.Improvement,
                SubmittedOn = f.SubmittedOn
            })
            .ToListAsync();

        var model = new EventFeedbackViewModel
        {
            Event = ev,
            Responses = responses,
            Attended = await _db.Registrations.CountAsync(r =>
                r.EventId == id && r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent)),
            RecommendCount = responses.Count(r => r.WouldRecommend),
            Distribution = Enumerable.Range(1, 5)
                .ToDictionary(score => score, score => responses.Count(r => r.OverallRating == score))
        };

        if (responses.Any())
        {
            model.Overall = responses.Average(r => r.OverallRating);
            model.Content = responses.Average(r => r.ContentRating);
            model.Venue = responses.Average(r => r.VenueRating);
            model.Organization = responses.Average(r => r.OrganizationRating);
        }

        return View(model);
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// The registration, but only if it belongs to the signed-in member. Every
    /// participant-side action goes through here, so one person can never open
    /// another's form by guessing an id.
    /// </summary>
    private async Task<Registration?> MyRegistrationAsync(int registrationId)
    {
        var user = await _users.GetUserAsync(User);

        if (user?.MemberId is null)
        {
            return null;
        }

        return await _db.Registrations
            .Include(r => r.Event)!.ThenInclude(e => e!.EventType)
            .Include(r => r.Event)!.ThenInclude(e => e!.Venue)
            .Include(r => r.Event)!.ThenInclude(e => e!.Organization)
            .Include(r => r.Event)!.ThenInclude(e => e!.Sessions)
            .Include(r => r.AttendanceRecords)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.MemberId == user.MemberId);
    }

    /// <summary>
    /// Why this registration cannot leave feedback, or null if it can. Same
    /// three rules the My Events screen shows the button by.
    /// </summary>
    private static string? Gate(Registration registration)
    {
        var ev = registration.Event;

        if (ev is null || !ev.CollectsFeedback)
        {
            return "That event is not collecting feedback.";
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return "Your registration for that event was cancelled.";
        }

        if (ev.EndsAt > DateTime.Now)
        {
            return "Feedback opens once the event is over.";
        }

        if (!registration.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent))
        {
            return "Feedback is for people who attended — there is no attendance on record for you.";
        }

        return null;
    }

    /// <summary>The event context around the form, plus any answers already given.</summary>
    private async Task<FeedbackFormViewModel> ShellAsync(Registration registration)
    {
        var ev = registration.Event!;

        var shell = new FeedbackFormViewModel
        {
            RegistrationId = registration.Id,
            EventId = ev.Id,
            EventTitle = ev.Title,
            TypeLabel = ev.EventType?.Name ?? "Event",
            ColorKey = ev.EventType?.ColorKey ?? "talk",
            OrgName = ev.Organization?.Name ?? string.Empty,
            VenueName = ev.Venue?.Name ?? string.Empty,
            StartsAt = ev.StartsAt,
            Sessions = ev.Sessions.Count,
            Attended = registration.AttendanceRecords.Count(a => a.Status != AttendanceStatus.Absent)
        };

        var existing = await _db.EventFeedback
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.RegistrationId == registration.Id);

        return existing is null
            ? shell
            : FeedbackFormViewModel.FromEntity(existing, shell);
    }
}
