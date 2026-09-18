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
/// Master data · Venues. Capacity here is the room's ceiling; an event may
/// take fewer, never more — the event form checks one against the other.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class VenuesController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public VenuesController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;

        var rows = await _db.Venues
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new VenueFormViewModel
            {
                Id = v.Id,
                Name = v.Name,
                Location = v.Location,
                Capacity = v.Capacity,
                AvailabilityNotes = v.AvailabilityNotes,
                ContactPerson = v.ContactPerson,
                ContactNumber = v.ContactNumber,
                IsActive = v.IsActive,
                UpcomingEvents = v.Events.Count(e => e.StartsAt >= now && e.Status != EventStatus.Cancelled)
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new VenueFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VenueFormViewModel model)
    {
        await GuardDuplicateNameAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var venue = new Venue();
        Apply(model, venue);

        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        AuditTrail.Record(_db, _users.GetUserId(User), "VenueCreated", nameof(Venue), venue.Id, venue.Name);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{venue.Name} was added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var venue = await _db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null)
        {
            return NotFound();
        }

        return View("Form", new VenueFormViewModel
        {
            Id = venue.Id,
            Name = venue.Name,
            Location = venue.Location,
            Capacity = venue.Capacity,
            AvailabilityNotes = venue.AvailabilityNotes,
            ContactPerson = venue.ContactPerson,
            ContactNumber = venue.ContactNumber,
            IsActive = venue.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VenueFormViewModel model)
    {
        var venue = await _db.Venues.FindAsync(id);
        if (venue is null)
        {
            return NotFound();
        }

        model.Id = id;
        await GuardDuplicateNameAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        Apply(model, venue);

        AuditTrail.Record(_db, _users.GetUserId(User), "VenueUpdated", nameof(Venue), venue.Id, venue.Name);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{venue.Name} was updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Taking a room out of service hides it from the event form. Events
    /// already booked into it keep their booking.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var venue = await _db.Venues.FindAsync(id);
        if (venue is null)
        {
            return NotFound();
        }

        venue.IsActive = !venue.IsActive;

        AuditTrail.Record(_db, _users.GetUserId(User), "VenueToggled", nameof(Venue), venue.Id,
            $"{venue.Name} set to {(venue.IsActive ? "bookable" : "out of service")}.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{venue.Name} is now {(venue.IsActive ? "bookable" : "out of service")}.";
        return RedirectToAction(nameof(Index));
    }

    private static void Apply(VenueFormViewModel model, Venue venue)
    {
        venue.Name = model.Name.Trim();
        venue.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        venue.Capacity = model.Capacity;
        venue.AvailabilityNotes = string.IsNullOrWhiteSpace(model.AvailabilityNotes) ? null : model.AvailabilityNotes.Trim();
        venue.ContactPerson = string.IsNullOrWhiteSpace(model.ContactPerson) ? null : model.ContactPerson.Trim();
        venue.ContactNumber = string.IsNullOrWhiteSpace(model.ContactNumber) ? null : model.ContactNumber.Trim();
        venue.IsActive = model.IsActive;
    }

    private async Task GuardDuplicateNameAsync(VenueFormViewModel model)
    {
        var name = model.Name.Trim();

        if (await _db.Venues.AnyAsync(v => v.Name == name && v.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Name), "There is already a venue with that name.");
        }
    }
}
