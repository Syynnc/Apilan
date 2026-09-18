using Apilan.Data;
using Apilan.Models;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Officer console home. Every block here is a queue that needs clearing or a
/// door that can be opened — the dashboard is a to-do list, not a poster.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class DashboardController : Controller
{
    private readonly ApilanDbContext _db;

    public DashboardController(ApilanDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var model = new DashboardViewModel
        {
            PublishedEvents = await _db.Events.CountAsync(e => e.Status == EventStatus.Published),
            DraftEvents = await _db.Events.CountAsync(e => e.Status == EventStatus.Draft),
            ActiveMembers = await _db.Members.CountAsync(m => m.Status == MembershipStatus.Active),
            PendingAccounts = await _db.Users.CountAsync(u => u.Status == AccountStatus.Pending),
            PendingRegistrations = await _db.Registrations.CountAsync(r => r.Status == RegistrationStatus.Pending),
            WaitlistedTotal = await _db.Registrations.CountAsync(r => r.Status == RegistrationStatus.Waitlisted),
            CertificatesEarned = await _db.Registrations.CountAsync(r =>
                r.Eligibility == EligibilityStatus.Eligible || r.Eligibility == EligibilityStatus.OverrideGranted),

            Today = await EventRows(e =>
                    e.StartsAt < tomorrow && e.EndsAt >= today &&
                    (e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing))
                .OrderBy(e => e.StartsAt)
                .ToListAsync(),

            Upcoming = await EventRows(e =>
                    e.StartsAt >= tomorrow && e.Status == EventStatus.Published)
                .OrderBy(e => e.StartsAt)
                .Take(5)
                .ToListAsync(),

            AwaitingCloseOut = await EventRows(e =>
                    e.EndsAt < DateTime.Now &&
                    (e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing))
                .OrderByDescending(e => e.StartsAt)
                .Take(5)
                .ToListAsync(),

            Recent = await _db.AuditLogs
                .AsNoTracking()
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
                .ToListAsync()
        };

        return View(model);
    }

    /// <summary>One projection, so every block on the page counts the same way.</summary>
    private IQueryable<DashboardEventRow> EventRows(
        System.Linq.Expressions.Expression<Func<Event, bool>> predicate) =>
        _db.Events
            .AsNoTracking()
            .Where(predicate)
            .Select(e => new DashboardEventRow
            {
                EventId = e.Id,
                Title = e.Title,
                ColorKey = e.EventType!.ColorKey,
                VenueName = e.Venue!.Name,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                Status = e.Status,
                Capacity = e.Capacity,
                Sessions = e.Sessions.Count,
                Taken = e.Registrations.Count(r =>
                    r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending),
                Present = e.Registrations.Count(r =>
                    r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent))
            });
}
