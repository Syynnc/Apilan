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
/// The public side of Transactions: what is on, and the one button that puts a
/// member on the list. Browsing is open to anyone — an org's calendar is
/// public — but taking a slot needs an approved account with a member record.
/// </summary>
[AllowAnonymous]
public class CalendarController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly RegistrationService _registrations;
    private readonly UserManager<ApplicationUser> _users;

    public CalendarController(
        ApilanDbContext db,
        RegistrationService registrations,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _registrations = registrations;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? typeId, string? search, int? organizationId)
    {
        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing)
            .Where(e => e.EndsAt >= DateTime.Now);

        if (typeId.HasValue)
        {
            query = query.Where(e => e.EventTypeId == typeId.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Title.Contains(term) || e.Description!.Contains(term));
        }

        var model = new CalendarViewModel
        {
            Events = await query.OrderBy(e => e.StartsAt).Select(Card).ToListAsync(),
            Types = await TypeOptionsAsync(),
            Organizations = await OrgOptionsAsync(),
            TypeId = typeId,
            OrganizationId = organizationId,
            Search = search
        };

        await FillMyRegistrationsAsync(model);
        return View(model);
    }

    // ---- Month grid -------------------------------------------------------

    /// <summary>
    /// The month view. Unlike the list, this shows the whole month including
    /// what has already happened — the question "what did we run in October"
    /// is as real as "what is coming up".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Month(int? year, int? month, DateTime? day, int? typeId, int? organizationId)
    {
        var anchor = SafeMonth(year, month);

        var from = anchor;
        var to = anchor.AddMonths(1);

        var events = await MonthQuery(from, to, typeId, organizationId)
            .OrderBy(e => e.StartsAt)
            .Select(Card)
            .ToListAsync();

        // An event spanning days shows on every day it runs — a two-day
        // assembly is on the calendar twice, because it happens twice.
        var days = new List<CalendarDay>();

        for (var d = 1; d <= DateTime.DaysInMonth(anchor.Year, anchor.Month); d++)
        {
            var date = new DateTime(anchor.Year, anchor.Month, d);

            days.Add(new CalendarDay
            {
                Date = date,
                Events = events
                    .Where(e => e.StartsAt.Date <= date && e.EndsAt.Date >= date)
                    .ToList()
            });
        }

        // Land on the day the visitor asked for, else the first day with
        // something on it, else the 1st — never an empty panel by default.
        var selected = day?.Date is { } asked && asked.Year == anchor.Year && asked.Month == anchor.Month
            ? asked
            : days.FirstOrDefault(x => x.HasEvents && x.Date >= DateTime.Today)?.Date
              ?? days.FirstOrDefault(x => x.HasEvents)?.Date
              ?? anchor;

        var model = new CalendarMonthViewModel
        {
            Year = anchor.Year,
            Month = anchor.Month,
            Days = days,
            Selected = selected,
            Types = await TypeOptionsAsync(),
            Organizations = await OrgOptionsAsync(),
            TypeId = typeId,
            OrganizationId = organizationId
        };

        var user = User.Identity?.IsAuthenticated == true ? await _users.GetUserAsync(User) : null;

        if (user?.MemberId is not null)
        {
            model.IsSignedInMember = true;
            model.MyRegistrations = await _db.Registrations
                .Where(r => r.MemberId == user.MemberId && r.Status != RegistrationStatus.Cancelled)
                .ToDictionaryAsync(r => r.EventId, r => r.Status);
        }

        return View(model);
    }

    /// <summary>
    /// The month as JSON, for the calendar widget on the landing page. Same
    /// query as <see cref="Month"/>, so the marketing page cannot drift out of
    /// step with the live calendar the way hard-coded sample data did.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Feed(int? year, int? month, int? typeId, int? organizationId)
    {
        var anchor = SafeMonth(year, month);

        // A month either side, so stepping through months in the widget does
        // not blank out while the next request is in flight.
        var from = anchor.AddMonths(-1);
        var to = anchor.AddMonths(2);

        var rows = await MonthQuery(from, to, typeId, organizationId)
            .OrderBy(e => e.StartsAt)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.StartsAt,
                e.EndsAt,
                Type = e.EventType!.ColorKey,
                Venue = e.Venue!.Name,
                Org = e.Organization!.Acronym,
                e.Capacity,
                Taken = e.Registrations.Count(r =>
                    r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending)
            })
            .ToListAsync();

        // Flattened per day, so the widget can group by date key without
        // working out which days a multi-day event covers.
        var entries = new List<CalendarFeedEntry>();

        foreach (var row in rows)
        {
            for (var date = row.StartsAt.Date; date <= row.EndsAt.Date; date = date.AddDays(1))
            {
                entries.Add(new CalendarFeedEntry(
                    date.ToString("yyyy-MM-dd"),
                    row.Title,
                    row.Type,
                    row.StartsAt.ToString("h:mm tt"),
                    row.Venue,
                    row.Org,
                    row.Taken,
                    row.Capacity,
                    row.Id));
            }
        }

        return Json(entries);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Organization)
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id &&
                (e.Status == EventStatus.Published ||
                 e.Status == EventStatus.Ongoing ||
                 e.Status == EventStatus.Completed));

        if (ev is null)
        {
            return NotFound();
        }

        var card = await _db.Events.AsNoTracking().Where(e => e.Id == id).Select(Card).FirstAsync();

        ViewData["Card"] = card;
        ViewData["MyStatus"] = await MyStatusAsync(id);

        return View(ev);
    }

    /// <summary>
    /// Takes a slot for the signed-in member. Officers register people from
    /// the event screen instead — this is the member's own action.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int id, string? returnUrl)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null || user.Status != AccountStatus.Active)
        {
            TempData["Error"] = "Your account is not active yet, so it cannot take a slot.";
            return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id })!);
        }

        if (user.MemberId is null)
        {
            TempData["Error"] = "Your account is not linked to a member record. Ask an officer to link it.";
            return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id })!);
        }

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
        {
            return NotFound();
        }

        var result = await _registrations.RegisterAsync(ev, user.MemberId.Value, isWalkIn: false, user.Id);

        if (result.Succeeded)
        {
            TempData["Success"] = $"{result.Message} Your code is {result.Registration!.Code}.";
            return RedirectToAction("Index", "MyEvents");
        }

        TempData["Error"] = result.Message;
        return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// The first of the month asked for, clamped to something sane. A hand-
    /// edited query string should give a boring answer, not a 500.
    /// </summary>
    private static DateTime SafeMonth(int? year, int? month)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : DateTime.Today.Month;

        return new DateTime(y, m, 1);
    }

    /// <summary>
    /// Published and past events overlapping a date window. Drafts never show
    /// publicly; cancelled ones drop off rather than sit there as dead rows.
    /// </summary>
    private IQueryable<Event> MonthQuery(DateTime from, DateTime to, int? typeId, int? organizationId)
    {
        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published
                     || e.Status == EventStatus.Ongoing
                     || e.Status == EventStatus.Completed)
            .Where(e => e.StartsAt < to && e.EndsAt >= from);

        if (typeId.HasValue)
        {
            query = query.Where(e => e.EventTypeId == typeId.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId.Value);
        }

        return query;
    }

    private async Task<List<EventTypeOption>> TypeOptionsAsync() =>
        await _db.EventTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new EventTypeOption(t.Id, t.Name, t.ColorKey))
            .ToListAsync();

    private async Task<List<OrganizationOption>> OrgOptionsAsync() =>
        await _db.Organizations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationOption(o.Id, o.Name, o.Acronym, o.ColorKey))
            .ToListAsync();

    /// <summary>
    /// One projection for the card, so the list and the detail page cannot
    /// disagree about how full an event is.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Event, CalendarCard>> Card => e => new CalendarCard
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        TypeLabel = e.EventType!.Name,
        ColorKey = e.EventType.ColorKey,
        OrgName = e.Organization!.Name,
        OrgAcronym = e.Organization.Acronym,
        VenueName = e.Venue!.Name,
        VenueLocation = e.Venue.Location,
        StartsAt = e.StartsAt,
        EndsAt = e.EndsAt,
        RegistrationDeadline = e.RegistrationDeadline,
        Capacity = e.Capacity,
        Taken = e.Registrations.Count(r =>
            r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending),
        Sessions = e.Sessions.Count,
        AllowWaitlist = e.AllowWaitlist,
        RequiresApproval = e.RequiresApproval,
        IssuesCertificate = e.IssuesCertificate,
        Status = e.Status
    };

    private async Task FillMyRegistrationsAsync(CalendarViewModel model)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await _users.GetUserAsync(User);
        if (user?.MemberId is null)
        {
            return;
        }

        model.IsSignedInMember = true;
        model.MyRegistrations = await _db.Registrations
            .Where(r => r.MemberId == user.MemberId && r.Status != RegistrationStatus.Cancelled)
            .ToDictionaryAsync(r => r.EventId, r => r.Status);
    }

    private async Task<RegistrationStatus?> MyStatusAsync(int eventId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var user = await _users.GetUserAsync(User);
        if (user?.MemberId is null)
        {
            return null;
        }

        var registration = await _db.Registrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.MemberId == user.MemberId);

        return registration?.Status;
    }
}
