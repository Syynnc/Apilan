using Apilan.Data;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Administration · Activity log. Read-only by design: a trail that can be
/// edited is not a trail. Every write in the system lands here through
/// <see cref="Services.AuditTrail"/>.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class AuditLogController : Controller
{
    private const int PageSize = 50;

    private readonly ApilanDbContext _db;

    public AuditLogController(ApilanDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? act, string? entityName, string? entityId, string? actorId,
        DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(act))
        {
            query = query.Where(a => a.Action == act);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }

        // Scoped to one record: "everything that ever happened to event 12".
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            query = query.Where(a => a.ActorUserId == actorId);
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.OccurredOn >= from.Value.Date);
        }

        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.OccurredOn <= end);
        }

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);

        var rows = await query
            .OrderByDescending(a => a.OccurredOn)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
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
            .ToListAsync();

        var actors = await _db.AuditLogs
            .Where(a => a.ActorUserId != null)
            .Select(a => new { a.ActorUserId, a.Actor!.FullName })
            .Distinct()
            .OrderBy(a => a.FullName)
            .ToListAsync();

        return View(new AuditLogViewModel
        {
            Rows = rows,
            Action = act,
            EntityName = entityName,
            EntityId = entityId,
            ActorId = actorId,
            From = from,
            To = to,
            Page = page,
            PageSize = PageSize,
            Total = total,
            Actions = await _db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(),
            Entities = await _db.AuditLogs.Select(a => a.EntityName).Distinct().OrderBy(a => a).ToListAsync(),
            Actors = actors.Select(a => (a.ActorUserId!, a.FullName)).ToList()
        });
    }
}
