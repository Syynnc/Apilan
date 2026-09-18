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
/// Master data · Organizations. Every event belongs to one org, which is what
/// lets the system answer "what has this org run, and how did it go" — the
/// question a council asks at the end of a term.
///
/// Any officer can add or edit an organization. There is no separate
/// administrator to reserve it for, and an officer setting up their own org is
/// the ordinary case rather than the exception.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class OrganizationsController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public OrganizationsController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // ---- List -------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool includeInactive = false)
    {
        var now = DateTime.Now;
        var query = _db.Organizations.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(o => o.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.Name.Contains(term) || o.Acronym.Contains(term));
        }

        var rows = await query
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationRow
            {
                Id = o.Id,
                Name = o.Name,
                Acronym = o.Acronym,
                Adviser = o.Adviser,
                ContactEmail = o.ContactEmail,
                ColorKey = o.ColorKey,
                IsActive = o.IsActive,
                CreatedOn = o.CreatedOn,
                TotalEvents = o.Events.Count,
                UpcomingEvents = o.Events.Count(e =>
                    e.StartsAt >= now && e.Status != EventStatus.Cancelled),
                Registrations = o.Events.SelectMany(e => e.Registrations)
                    .Count(r => r.Status != RegistrationStatus.Cancelled),
                Officers = _db.Users.Count(u => u.OrganizationId == o.Id)
            })
            .ToListAsync();

        return View(new OrganizationListViewModel
        {
            Organizations = rows,
            Search = search,
            IncludeInactive = includeInactive,
            ActiveCount = await _db.Organizations.CountAsync(o => o.IsActive),
            InactiveCount = await _db.Organizations.CountAsync(o => !o.IsActive)
        });
    }

    // ---- What this org has run --------------------------------------------

    /// <summary>
    /// One org's own history. This is the "view events created by each org"
    /// screen: its events, its turnout, and what participants said about them.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, EventStatus? status)
    {
        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (org is null)
        {
            return NotFound();
        }

        var eventQuery = _db.Events.AsNoTracking().Where(e => e.OrganizationId == id);

        if (status.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.Status == status.Value);
        }

        var events = await eventQuery
            .OrderByDescending(e => e.StartsAt)
            .Select(e => new EventRowViewModel
            {
                Id = e.Id,
                Title = e.Title,
                TypeLabel = e.EventType!.Name,
                ColorKey = e.EventType.ColorKey,
                OrganizationId = e.OrganizationId,
                OrgAcronym = org.Acronym,
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

        // Turnout across everything the org has run, whatever the filter shows.
        var registrations = _db.Registrations.Where(r =>
            r.Event!.OrganizationId == id && r.Status != RegistrationStatus.Cancelled);

        var ratings = await _db.EventFeedback
            .Where(f => f.Registration!.Event!.OrganizationId == id)
            .Select(f => (f.OverallRating + f.ContentRating + f.VenueRating + f.OrganizationRating) / 4.0)
            .ToListAsync();

        var officers = await _db.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == id)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync();

        var officerRows = new List<OrganizationOfficerRow>();
        foreach (var officer in officers)
        {
            var user = await _users.FindByIdAsync(officer.Id);
            var roles = user is null ? new List<string>() : (await _users.GetRolesAsync(user)).ToList();

            officerRows.Add(new OrganizationOfficerRow(
                officer.Id,
                officer.FullName,
                officer.Email,
                roles.FirstOrDefault() ?? "—"));
        }

        return View(new OrganizationDetailsViewModel
        {
            Organization = org,
            Events = events,
            Officers = officerRows,
            Status = status,
            TotalEvents = await _db.Events.CountAsync(e => e.OrganizationId == id),
            Registrations = await registrations.CountAsync(),
            Attended = await registrations.CountAsync(r =>
                r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent)),
            Certificates = await registrations.CountAsync(r =>
                r.Eligibility == EligibilityStatus.Eligible ||
                r.Eligibility == EligibilityStatus.OverrideGranted),
            Responses = ratings.Count,
            AverageRating = ratings.Any() ? ratings.Average() : null
        });
    }

    // ---- Create / edit ----------------------------------------------------

    [HttpGet]
    public IActionResult Create() => View("Form", new OrganizationFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrganizationFormViewModel model)
    {
        await GuardDuplicatesAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var org = new Organization { CreatedOn = DateTime.Now };
        model.ApplyTo(org);

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();

        AuditTrail.Record(_db, _users.GetUserId(User), "OrganizationCreated", nameof(Organization),
            org.Id, $"{org.Acronym} — {org.Name}");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{org.Acronym} was added. Its events can now be filed against it.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);

        return org is null
            ? NotFound()
            : View("Form", OrganizationFormViewModel.FromEntity(org));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrganizationFormViewModel model)
    {
        var org = await _db.Organizations.FindAsync(id);
        if (org is null)
        {
            return NotFound();
        }

        model.Id = id;
        await GuardDuplicatesAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        model.ApplyTo(org);

        AuditTrail.Record(_db, _users.GetUserId(User), "OrganizationUpdated", nameof(Organization),
            org.Id, $"{org.Acronym} — {org.Name}");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{org.Acronym} was updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Archiving an org hides it from the event form and the public filter.
    /// Its events and their history stay exactly where they are — a term's
    /// reports do not stop being true because the org wound down.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var org = await _db.Organizations.FindAsync(id);
        if (org is null)
        {
            return NotFound();
        }

        org.IsActive = !org.IsActive;

        AuditTrail.Record(_db, _users.GetUserId(User), "OrganizationToggled", nameof(Organization),
            org.Id, $"{org.Acronym} set to {(org.IsActive ? "active" : "archived")}.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{org.Acronym} is now {(org.IsActive ? "active" : "archived")}.";
        return RedirectToAction(nameof(Index));
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task GuardDuplicatesAsync(OrganizationFormViewModel model)
    {
        var name = model.Name.Trim();
        var acronym = model.Acronym.Trim().ToUpperInvariant();

        if (await _db.Organizations.AnyAsync(o => o.Name == name && o.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Name), "There is already an organization with that name.");
        }

        if (await _db.Organizations.AnyAsync(o => o.Acronym == acronym && o.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Acronym), "That acronym is taken.");
        }
    }
}
