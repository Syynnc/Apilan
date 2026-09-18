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
/// Transactions · Events. An event moves Draft → Published → Ongoing →
/// Completed; only a published event takes registrations, and only a completed
/// one settles certificates. The roster and its actions live here too, since
/// running an event is one job rather than four screens.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class EventsController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly RegistrationService _registrations;
    private readonly UserManager<ApplicationUser> _users;

    public EventsController(
        ApilanDbContext db,
        RegistrationService registrations,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _registrations = registrations;
        _users = users;
    }

    // ---- List -------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(EventStatus? status, string? search, int? organizationId)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Title.Contains(term));
        }

        var rows = await query
            .OrderByDescending(e => e.StartsAt)
            .Select(e => new EventRowViewModel
            {
                Id = e.Id,
                Title = e.Title,
                TypeLabel = e.EventType!.Name,
                ColorKey = e.EventType.ColorKey,
                OrganizationId = e.OrganizationId,
                OrgAcronym = e.Organization!.Acronym,
                VenueName = e.Venue!.Name,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                RegistrationDeadline = e.RegistrationDeadline,
                Capacity = e.Capacity,
                Taken = e.Registrations.Count(r =>
                    r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending),
                Waitlisted = e.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
                Sessions = e.Sessions.Count,
                Status = e.Status
            })
            .ToListAsync();

        return View(new EventListViewModel
        {
            Events = rows,
            Status = status,
            Search = search,
            OrganizationId = organizationId,
            Organizations = await OrganizationOptionsAsync(),
            DraftCount = await _db.Events.CountAsync(e => e.Status == EventStatus.Draft),
            PublishedCount = await _db.Events.CountAsync(e => e.Status == EventStatus.Published),
            CompletedCount = await _db.Events.CountAsync(e => e.Status == EventStatus.Completed)
        });
    }

    // ---- Create / edit ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new EventFormViewModel();
        model.Sessions.Add(new SessionFormRow
        {
            Name = "Main session",
            StartsAt = model.StartsAt,
            EndsAt = model.EndsAt
        });

        // An officer almost always runs their own org's event, so start there.
        var user = await _users.GetUserAsync(User);
        if (user?.OrganizationId is not null)
        {
            model.OrganizationId = user.OrganizationId.Value;
        }

        await FillPickListsAsync(model);

        // A one-org install should not have to pick itself out of a list of one.
        if (model.OrganizationId == 0 && model.Organizations.Count == 1)
        {
            model.OrganizationId = model.Organizations[0].Id;
        }

        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        await ValidateAgainstVenueAsync(model);
        await ValidateOrganizationAsync(model);

        if (!ModelState.IsValid)
        {
            await FillPickListsAsync(model);
            return View("Form", model);
        }

        var ev = new Event
        {
            Status = EventStatus.Draft,
            CreatedOn = DateTime.Now,
            CreatedByUserId = _users.GetUserId(User)
        };

        Apply(model, ev);
        SyncSessions(model, ev);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var org = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ev.OrganizationId);

        AuditTrail.Record(_db, _users.GetUserId(User), "EventCreated", nameof(Event), ev.Id,
            $"{ev.Title} for {org?.Acronym}.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{ev.Title}\" was saved as a draft. Publish it when registration should open.";
        return RedirectToAction(nameof(Details), new { id = ev.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        var attendanceBySession = await _db.AttendanceRecords
            .Where(a => a.EventSession!.EventId == id)
            .GroupBy(a => a.EventSessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);

        var model = new EventFormViewModel
        {
            Id = ev.Id,
            Title = ev.Title,
            Description = ev.Description,
            OrganizationId = ev.OrganizationId,
            EventTypeId = ev.EventTypeId,
            VenueId = ev.VenueId,
            StartsAt = ev.StartsAt,
            EndsAt = ev.EndsAt,
            RegistrationDeadline = ev.RegistrationDeadline,
            Capacity = ev.Capacity,
            RequiresApproval = ev.RequiresApproval,
            AllowWaitlist = ev.AllowWaitlist,
            IssuesCertificate = ev.IssuesCertificate,
            CollectsFeedback = ev.CollectsFeedback,
            CertificateThresholdPercent = ev.CertificateThresholdPercent,
            Status = ev.Status,
            Sessions = ev.Sessions
                .OrderBy(s => s.Sequence)
                .Select(s => new SessionFormRow
                {
                    Id = s.Id,
                    Name = s.Name,
                    StartsAt = s.StartsAt,
                    EndsAt = s.EndsAt,
                    LateAfterMinutes = s.LateAfterMinutes,
                    AttendanceCount = attendanceBySession.TryGetValue(s.Id, out var c) ? c : 0
                })
                .ToList()
        };

        await FillPickListsAsync(model);
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        var ev = await _db.Events
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        model.Id = id;
        model.Status = ev.Status;

        await ValidateAgainstVenueAsync(model);
        await ValidateOrganizationAsync(model);
        await ValidateCapacityAgainstRosterAsync(model, ev);
        await ValidateSessionRemovalsAsync(model);

        if (!ModelState.IsValid)
        {
            await FillPickListsAsync(model);
            return View("Form", model);
        }

        // What actually moved, so the trail says more than "it was edited".
        var changes = Describe(model, ev);

        Apply(model, ev);
        SyncSessions(model, ev);

        ev.LastEditedOn = DateTime.Now;
        ev.LastEditedByUserId = _users.GetUserId(User);

        AuditTrail.Record(_db, _users.GetUserId(User), "EventUpdated", nameof(Event), ev.Id,
            changes.Any() ? $"{ev.Title} — {string.Join("; ", changes)}." : ev.Title);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{ev.Title}\" was updated.";
        return RedirectToAction(nameof(Details), new { id = ev.Id });
    }

    // ---- Detail -----------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? rosterFilter, string? search)
    {
        var ev = await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Organization)
            .Include(e => e.Sessions)
            .Include(e => e.CreatedBy)
            .Include(e => e.LastEditedBy)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        var sessionCount = ev.Sessions.Count;

        var rosterQuery = _db.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rosterQuery = rosterQuery.Where(r =>
                r.Member!.LastName.Contains(term) ||
                r.Member.FirstName.Contains(term) ||
                r.Member.StudentNumber.Contains(term) ||
                r.Code.Contains(term));
        }

        if (Enum.TryParse<RegistrationStatus>(rosterFilter, out var wanted))
        {
            rosterQuery = rosterQuery.Where(r => r.Status == wanted);
        }

        var roster = await rosterQuery
            .OrderBy(r => r.Status == RegistrationStatus.Waitlisted ? r.WaitlistPosition : 0)
            .ThenBy(r => r.Member!.LastName).ThenBy(r => r.Member!.FirstName)
            .Select(r => new RosterRow
            {
                RegistrationId = r.Id,
                MemberId = r.MemberId,
                Code = r.Code,
                ListName = r.Member!.LastName + ", " + r.Member.FirstName,
                StudentNumber = r.Member.StudentNumber,
                CourseYear = r.Member.Course + " · " + r.Member.YearLevel,
                Status = r.Status,
                WaitlistPosition = r.WaitlistPosition,
                IsWalkIn = r.IsWalkIn,
                RegisteredOn = r.RegisteredOn,
                Attended = r.AttendanceRecords.Count(a => a.Status != AttendanceStatus.Absent),
                Late = r.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Late),
                Absent = r.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Absent),
                Sessions = sessionCount,
                Eligibility = r.Eligibility,
                Remarks = r.Remarks,
                HasFeedback = r.Feedback != null
            })
            .ToListAsync();

        var sessions = await SessionSummaries.ForEventAsync(_db, id);

        var onRoster = await _db.Registrations
            .Where(r => r.EventId == id && r.Status != RegistrationStatus.Cancelled)
            .Select(r => r.MemberId)
            .ToListAsync();

        var model = new EventDetailsViewModel
        {
            Event = ev,
            Roster = roster,
            Sessions = sessions,
            RosterFilter = rosterFilter,
            Search = search,
            Confirmed = await _db.Registrations.CountAsync(r => r.EventId == id && r.Status == RegistrationStatus.Confirmed),
            Pending = await _db.Registrations.CountAsync(r => r.EventId == id && r.Status == RegistrationStatus.Pending),
            Waitlisted = await _db.Registrations.CountAsync(r => r.EventId == id && r.Status == RegistrationStatus.Waitlisted),
            Cancelled = await _db.Registrations.CountAsync(r => r.EventId == id && r.Status == RegistrationStatus.Cancelled),
            WalkIns = await _db.Registrations.CountAsync(r => r.EventId == id && r.IsWalkIn),
            AttendanceRecords = await _db.AttendanceRecords
                .CountAsync(a => a.EventSession!.EventId == id),
            FeedbackResponses = await _db.EventFeedback
                .CountAsync(f => f.Registration!.EventId == id),
            FeedbackAverage = await _db.EventFeedback
                .Where(f => f.Registration!.EventId == id)
                .Select(f => (double?)(
                    f.OverallRating + f.ContentRating + f.VenueRating + f.OrganizationRating) / 4.0)
                .AverageAsync(),
            History = await _db.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(Event) && a.EntityId == id.ToString())
                .OrderByDescending(a => a.OccurredOn)
                .Take(8)
                .Select(a => new AuditRow
                {
                    Id = a.Id,
                    OccurredOn = a.OccurredOn,
                    ActorName = a.Actor!.FullName,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Detail = a.Detail
                })
                .ToListAsync(),
            IsOpen = RegistrationService.IsOpenForRegistration(ev),
            AddableMembers = await _db.Members
                .Where(m => m.Status == MembershipStatus.Active && !onRoster.Contains(m.Id))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .Select(m => new MemberOption(m.Id, m.LastName + ", " + m.FirstName, m.StudentNumber))
                .ToListAsync()
        };

        return View(model);
    }

    // ---- Lifecycle --------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var ev = await _db.Events.Include(e => e.Sessions).FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
        {
            return NotFound();
        }

        if (ev.Status != EventStatus.Draft)
        {
            TempData["Error"] = "Only a draft can be published.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!ev.Sessions.Any())
        {
            TempData["Error"] = "Add at least one session before publishing — attendance is taken per session.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        ev.Status = EventStatus.Published;
        ev.PublishedOn = DateTime.Now;

        AuditTrail.Record(_db, _users.GetUserId(User), "EventPublished", nameof(Event), ev.Id, ev.Title);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{ev.Title}\" is live on the calendar and taking registrations.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Closing out an event settles every certificate decision against the
    /// attendance on record, so the report and the certificates agree.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Sessions)
            .Include(e => e.Registrations)!.ThenInclude(r => r.AttendanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        if (ev.Status is not (EventStatus.Published or EventStatus.Ongoing))
        {
            TempData["Error"] = "Only a published or ongoing event can be closed out.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var settled = SettleEligibility(ev);

        ev.Status = EventStatus.Completed;

        AuditTrail.Record(_db, _users.GetUserId(User), "EventCompleted", nameof(Event), ev.Id,
            $"{ev.Title}. {settled} certificate decision(s) settled.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{ev.Title}\" is closed out. {settled} certificate decision(s) settled.";
        return RedirectToAction("Event", "Certificates", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
        {
            return NotFound();
        }

        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            TempData["Error"] = "That event is already closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ev.Status = EventStatus.Cancelled;

        // Everyone's stub is void, including the queue.
        var registrations = await _db.Registrations
            .Where(r => r.EventId == id && r.Status != RegistrationStatus.Cancelled)
            .ToListAsync();

        foreach (var registration in registrations)
        {
            registration.Status = RegistrationStatus.Cancelled;
            registration.WaitlistPosition = null;
            registration.Remarks = string.IsNullOrWhiteSpace(reason) ? "Event cancelled." : reason.Trim();
        }

        AuditTrail.Record(_db, _users.GetUserId(User), "EventCancelled", nameof(Event), ev.Id,
            $"{ev.Title}. {registrations.Count} registration(s) voided. {reason}".Trim());
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{ev.Title}\" was cancelled and {registrations.Count} registration(s) were voided.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Removes an event outright. This is for the draft nobody joined and the
    /// mistake made five minutes ago — not for undoing history. An event with
    /// any attendance behind it is a fact the reports depend on, so it gets
    /// cancelled instead and the button is not offered.
    ///
    /// Its sessions and registrations go with it by cascade. The audit entry
    /// stays: the trail outlives the row, which is the point of a trail.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? confirmTitle)
    {
        var ev = await _db.Events
            .Include(e => e.Organization)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return NotFound();
        }

        if (ev.Status is not (EventStatus.Draft or EventStatus.Cancelled))
        {
            TempData["Error"] = "Only a draft or a cancelled event can be deleted. Cancel it first — "
                              + "that voids the stubs and keeps the history.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var attendance = await _db.AttendanceRecords.CountAsync(a => a.EventSession!.EventId == id);

        if (attendance > 0)
        {
            TempData["Error"] = $"{attendance} attendance record(s) are on file, so this event is part "
                              + "of the org's history and cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Typing the title is the confirmation — a modal is easy to click past
        // when you are tired, and this deletes a roster.
        if (!string.Equals(confirmTitle?.Trim(), ev.Title, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "The title did not match, so nothing was deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var title = ev.Title;
        var voided = ev.Registrations.Count;

        // The log line has to survive the row it describes, so it names the
        // event in its detail rather than pointing at an id that will be gone.
        AuditTrail.Record(_db, _users.GetUserId(User), "EventDeleted", nameof(Event), ev.Id,
            $"\"{title}\" ({ev.Organization?.Acronym}) on {ev.StartsAt:d MMM yyyy} deleted with "
            + $"{voided} registration(s). No attendance was on file.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{title}\" was deleted along with {voided} registration(s).";
        return RedirectToAction(nameof(Index));
    }

    // ---- Roster actions ---------------------------------------------------

    /// <summary>Adds someone at the door, or ahead of it, without a self-service account.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParticipant(int id, int memberId, bool walkIn = true)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
        {
            return NotFound();
        }

        var result = await _registrations.RegisterAsync(ev, memberId, walkIn, _users.GetUserId(User));

        if (result.Succeeded)
        {
            TempData["Success"] = $"Added. Check-in code {result.Registration!.Code}.";
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRegistration(int id, int registrationId)
    {
        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (registration is null)
        {
            return NotFound();
        }

        if (registration.Status != RegistrationStatus.Pending)
        {
            TempData["Error"] = "Only a pending registration can be approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        registration.Status = RegistrationStatus.Confirmed;

        AuditTrail.Record(_db, _users.GetUserId(User), "RegistrationApproved", nameof(Registration), registration.Code,
            registration.Member?.ListName);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{registration.Member?.FullName} is confirmed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRegistration(int id, int registrationId, string? reason)
    {
        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (registration is null)
        {
            return NotFound();
        }

        var promoted = await _registrations.CancelAsync(registration, _users.GetUserId(User), reason);

        TempData["Success"] = promoted is null
            ? $"{registration.Member?.FullName}'s registration was cancelled."
            : $"{registration.Member?.FullName}'s registration was cancelled; the freed slot went to the top of the waitlist.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteRegistration(int id, int registrationId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        var registration = await _db.Registrations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.EventId == id);

        if (ev is null || registration is null)
        {
            return NotFound();
        }

        if (registration.Status != RegistrationStatus.Waitlisted)
        {
            TempData["Error"] = "Only a waitlisted registration can be promoted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var taken = await _registrations.TakenSlotsAsync(id);
        if (taken >= ev.Capacity)
        {
            TempData["Error"] = "There is no free slot. Raise the capacity or cancel someone first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        registration.Status = ev.RequiresApproval ? RegistrationStatus.Pending : RegistrationStatus.Confirmed;
        registration.WaitlistPosition = null;

        AuditTrail.Record(_db, _users.GetUserId(User), "WaitlistPromoted", nameof(Registration), registration.Code,
            registration.Member?.ListName);

        await _registrations.ResequenceWaitlistAsync(id);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{registration.Member?.FullName} was moved off the waitlist.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// Writes the rule's verdict onto every registration that an officer has
    /// not overridden. Shared by Complete and the Certificates screen.
    /// </summary>
    internal static int SettleEligibility(Event ev)
    {
        var sessionCount = ev.Sessions.Count;
        var settled = 0;

        foreach (var registration in ev.Registrations)
        {
            var resolved = AttendanceRules.Resolve(
                registration.Eligibility,
                registration.Status,
                registration.AttendanceRecords.Count(a => AttendanceRules.CountsAsAttended(a.Status)),
                sessionCount,
                ev.CertificateThresholdPercent);

            if (resolved != registration.Eligibility)
            {
                registration.Eligibility = resolved;
            }

            settled++;
        }

        return settled;
    }

    private static void Apply(EventFormViewModel model, Event ev)
    {
        ev.Title = model.Title.Trim();
        ev.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        ev.OrganizationId = model.OrganizationId;
        ev.EventTypeId = model.EventTypeId;
        ev.VenueId = model.VenueId;
        ev.StartsAt = model.StartsAt;
        ev.EndsAt = model.EndsAt;
        ev.RegistrationDeadline = model.RegistrationDeadline;
        ev.Capacity = model.Capacity;
        ev.RequiresApproval = model.RequiresApproval;
        ev.AllowWaitlist = model.AllowWaitlist;
        ev.IssuesCertificate = model.IssuesCertificate;
        ev.CollectsFeedback = model.CollectsFeedback;
        ev.CertificateThresholdPercent = model.CertificateThresholdPercent;
    }

    /// <summary>
    /// What an edit actually changed, in words, for the audit trail. "Someone
    /// edited this event" is not much of a record; "moved the venue and cut
    /// capacity from 180 to 60" is what an officer needs to see next term.
    /// </summary>
    private static List<string> Describe(EventFormViewModel model, Event ev)
    {
        var changes = new List<string>();

        void Note(string what, object? before, object? after)
        {
            if (!Equals(before?.ToString(), after?.ToString()))
            {
                changes.Add($"{what} {before} → {after}");
            }
        }

        Note("title", ev.Title, model.Title.Trim());
        Note("organization", ev.OrganizationId, model.OrganizationId);
        Note("type", ev.EventTypeId, model.EventTypeId);
        Note("venue", ev.VenueId, model.VenueId);
        Note("start", ev.StartsAt.ToString("g"), model.StartsAt.ToString("g"));
        Note("end", ev.EndsAt.ToString("g"), model.EndsAt.ToString("g"));
        Note("deadline", ev.RegistrationDeadline.ToString("g"), model.RegistrationDeadline.ToString("g"));
        Note("capacity", ev.Capacity, model.Capacity);
        Note("threshold", ev.CertificateThresholdPercent, model.CertificateThresholdPercent);

        var live = model.Sessions.Count(s => !s.IsDeleted && !string.IsNullOrWhiteSpace(s.Name));
        Note("sessions", ev.Sessions.Count, live);

        return changes;
    }

    /// <summary>
    /// Applies the posted session rows onto the event: updates what is still
    /// there, adds what is new, drops what was removed, and renumbers.
    /// </summary>
    private void SyncSessions(EventFormViewModel model, Event ev)
    {
        var posted = model.Sessions
            .Where(s => !s.IsDeleted && !string.IsNullOrWhiteSpace(s.Name))
            .ToList();

        var keptIds = posted.Where(s => s.Id != 0).Select(s => s.Id).ToHashSet();

        foreach (var gone in ev.Sessions.Where(s => !keptIds.Contains(s.Id)).ToList())
        {
            ev.Sessions.Remove(gone);
            _db.EventSessions.Remove(gone);
        }

        var sequence = 1;
        foreach (var row in posted.OrderBy(s => s.StartsAt))
        {
            var session = row.Id == 0
                ? null
                : ev.Sessions.FirstOrDefault(s => s.Id == row.Id);

            if (session is null)
            {
                session = new EventSession();
                ev.Sessions.Add(session);
            }

            session.Name = row.Name.Trim();
            session.StartsAt = row.StartsAt;
            session.EndsAt = row.EndsAt;
            session.LateAfterMinutes = row.LateAfterMinutes;
            session.Sequence = sequence++;
        }
    }

    private async Task<List<OrganizationOption>> OrganizationOptionsAsync(int? keep = null) =>
        await _db.Organizations
            .Where(o => o.IsActive || o.Id == keep)
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationOption(o.Id, o.Name, o.Acronym, o.ColorKey))
            .ToListAsync();

    private async Task FillPickListsAsync(EventFormViewModel model)
    {
        model.Organizations = await OrganizationOptionsAsync(model.OrganizationId);

        model.EventTypes = await _db.EventTypes
            .Where(t => t.IsActive || t.Id == model.EventTypeId)
            .OrderBy(t => t.Name)
            .Select(t => new EventTypeOption(t.Id, t.Name, t.ColorKey))
            .ToListAsync();

        model.Venues = await _db.Venues
            .Where(v => v.IsActive || v.Id == model.VenueId)
            .OrderBy(v => v.Name)
            .Select(v => new VenueOption(v.Id, v.Name, v.Capacity, v.Location))
            .ToListAsync();

        model.SelectedVenueCapacity = model.Venues
            .FirstOrDefault(v => v.Id == model.VenueId)?.Capacity ?? 0;
    }

    /// <summary>
    /// An event has to belong to an organization that exists and is still
    /// running. Editing an event already filed against an archived org leaves
    /// it there — the history is not wrong just because the org wound down.
    /// </summary>
    private async Task ValidateOrganizationAsync(EventFormViewModel model)
    {
        var org = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == model.OrganizationId);

        if (org is null)
        {
            ModelState.AddModelError(nameof(model.OrganizationId), "Pick the organization running this.");
            return;
        }

        var wasAlready = model.Id != 0 && await _db.Events
            .AnyAsync(e => e.Id == model.Id && e.OrganizationId == model.OrganizationId);

        if (!org.IsActive && !wasAlready)
        {
            ModelState.AddModelError(nameof(model.OrganizationId),
                $"{org.Acronym} is archived, so new events cannot be filed against it.");
        }
    }

    /// <summary>The room is a hard ceiling — an event cannot seat more than it holds.</summary>
    private async Task ValidateAgainstVenueAsync(EventFormViewModel model)
    {
        var venue = await _db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == model.VenueId);

        if (venue is null)
        {
            ModelState.AddModelError(nameof(model.VenueId), "Pick a venue.");
            return;
        }

        if (model.Capacity > venue.Capacity)
        {
            ModelState.AddModelError(nameof(model.Capacity),
                $"{venue.Name} holds {venue.Capacity}. Lower the capacity or book a bigger room.");
        }
    }

    /// <summary>Capacity cannot be cut below the people already holding slots.</summary>
    private async Task ValidateCapacityAgainstRosterAsync(EventFormViewModel model, Event ev)
    {
        var taken = await _registrations.TakenSlotsAsync(ev.Id);

        if (model.Capacity < taken)
        {
            ModelState.AddModelError(nameof(model.Capacity),
                $"{taken} people already hold a slot. Cancel some registrations before cutting capacity that far.");
        }
    }

    /// <summary>A session people were marked present at cannot quietly disappear.</summary>
    private async Task ValidateSessionRemovalsAsync(EventFormViewModel model)
    {
        var removedIds = model.Sessions
            .Where(s => s.IsDeleted && s.Id != 0)
            .Select(s => s.Id)
            .ToList();

        if (!removedIds.Any())
        {
            return;
        }

        var withAttendance = await _db.AttendanceRecords
            .Where(a => removedIds.Contains(a.EventSessionId))
            .Select(a => a.EventSession!.Name)
            .Distinct()
            .ToListAsync();

        foreach (var name in withAttendance)
        {
            ModelState.AddModelError(nameof(model.Sessions),
                $"\"{name}\" already has attendance recorded, so it cannot be removed.");
        }
    }
}
