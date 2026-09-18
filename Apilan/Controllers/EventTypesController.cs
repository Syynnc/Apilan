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
/// Master data · Event types. A type carries the colour key that drives the
/// ticket spine and the calendar pip, so the classification an officer picks
/// is the same thing a member sees on the stub.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class EventTypesController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public EventTypesController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = await _db.EventTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new EventTypeFormViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                ColorKey = t.ColorKey,
                IsActive = t.IsActive,
                EventCount = t.Events.Count
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new EventTypeFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventTypeFormViewModel model)
    {
        await GuardDuplicateNameAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var type = new EventType
        {
            Name = model.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            ColorKey = model.ColorKey,
            IsActive = model.IsActive
        };

        _db.EventTypes.Add(type);
        await _db.SaveChangesAsync();

        AuditTrail.Record(_db, _users.GetUserId(User), "EventTypeCreated", nameof(EventType), type.Id, type.Name);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{type.Name}\" was added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var type = await _db.EventTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new EventTypeFormViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                ColorKey = t.ColorKey,
                IsActive = t.IsActive,
                EventCount = t.Events.Count
            })
            .FirstOrDefaultAsync();

        return type is null ? NotFound() : View("Form", type);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventTypeFormViewModel model)
    {
        var type = await _db.EventTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        model.Id = id;
        await GuardDuplicateNameAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        type.Name = model.Name.Trim();
        type.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        type.ColorKey = model.ColorKey;
        type.IsActive = model.IsActive;

        AuditTrail.Record(_db, _users.GetUserId(User), "EventTypeUpdated", nameof(EventType), type.Id, type.Name);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{type.Name}\" was updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Retiring a type hides it from the event form without touching the
    /// events already classified under it.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var type = await _db.EventTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        type.IsActive = !type.IsActive;

        AuditTrail.Record(_db, _users.GetUserId(User), "EventTypeToggled", nameof(EventType), type.Id,
            $"{type.Name} set to {(type.IsActive ? "in use" : "retired")}.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"\"{type.Name}\" is now {(type.IsActive ? "in use" : "retired")}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task GuardDuplicateNameAsync(EventTypeFormViewModel model)
    {
        var name = model.Name.Trim();

        if (await _db.EventTypes.AnyAsync(t => t.Name == name && t.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Name), "There is already an event type with that name.");
        }
    }
}
