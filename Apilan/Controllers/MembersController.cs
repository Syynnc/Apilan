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
/// Master data · Members. The roster is the organization's own record of a
/// person, kept separately from login accounts: officers maintain records for
/// people who never sign up online, and walk-ins are registered at the door.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class MembersController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MembersController(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, MembershipStatus? status, MembershipType? type, string? course)
    {
        var query = _db.Members.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m =>
                m.LastName.Contains(term) ||
                m.FirstName.Contains(term) ||
                m.StudentNumber.Contains(term) ||
                (m.Email != null && m.Email.Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(m => m.MembershipType == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(course))
        {
            query = query.Where(m => m.Course == course);
        }

        var accountMemberIds = await _db.Users
            .Where(u => u.MemberId != null)
            .Select(u => u.MemberId!.Value)
            .ToListAsync();

        var rows = await query
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Select(m => new MemberRowViewModel
            {
                Id = m.Id,
                StudentNumber = m.StudentNumber,
                ListName = m.LastName + ", " + m.FirstName,
                Course = m.Course,
                YearLevel = m.YearLevel,
                Section = m.Section,
                Email = m.Email,
                ContactNumber = m.ContactNumber,
                MembershipType = m.MembershipType,
                Status = m.Status,
                JoinedOn = m.JoinedOn,
                Registrations = m.Registrations.Count
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            row.HasAccount = accountMemberIds.Contains(row.Id);
        }

        var model = new MemberListViewModel
        {
            Members = rows,
            Search = search,
            Status = status,
            Type = type,
            Course = course,
            Courses = await _db.Members.Select(m => m.Course).Distinct().OrderBy(c => c).ToListAsync(),
            ActiveCount = await _db.Members.CountAsync(m => m.Status == MembershipStatus.Active),
            InactiveCount = await _db.Members.CountAsync(m => m.Status == MembershipStatus.Inactive)
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new MemberFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MemberFormViewModel model)
    {
        await GuardDuplicateStudentNumberAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var member = new Member();
        model.ApplyTo(member);

        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        AuditTrail.Record(_db, _users.GetUserId(User), "MemberCreated", nameof(Member), member.Id,
            $"{member.ListName} ({member.StudentNumber}).");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{member.FullName} was added to the roster.";
        return RedirectToAction(nameof(Details), new { id = member.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var member = await _db.Members.FindAsync(id);
        return member is null ? NotFound() : View("Form", MemberFormViewModel.FromEntity(member));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MemberFormViewModel model)
    {
        var member = await _db.Members.FindAsync(id);
        if (member is null)
        {
            return NotFound();
        }

        model.Id = id;
        await GuardDuplicateStudentNumberAsync(model);

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        model.ApplyTo(member);

        AuditTrail.Record(_db, _users.GetUserId(User), "MemberUpdated", nameof(Member), member.Id,
            $"{member.ListName} ({member.StudentNumber}).");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{member.FullName}'s record was updated.";
        return RedirectToAction(nameof(Details), new { id = member.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (member is null)
        {
            return NotFound();
        }

        var account = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.MemberId == id);

        var history = await _db.Registrations
            .AsNoTracking()
            .Where(r => r.MemberId == id)
            .OrderByDescending(r => r.Event!.StartsAt)
            .Select(r => new MemberHistoryRow
            {
                EventId = r.EventId,
                Title = r.Event!.Title,
                StartsAt = r.Event.StartsAt,
                TypeLabel = r.Event.EventType!.Name,
                Status = r.Status,
                Attended = r.AttendanceRecords.Count(a => a.Status != AttendanceStatus.Absent),
                Sessions = r.Event.Sessions.Count,
                Eligibility = r.Eligibility
            })
            .ToListAsync();

        foreach (var row in history)
        {
            row.Earned = AttendanceRules.IsEarned(row.Eligibility);
        }

        return View(new MemberDetailsViewModel
        {
            Member = member,
            HasAccount = account is not null,
            AccountStatus = account?.Status,
            AccountEmail = account?.Email,
            History = history
        });
    }

    /// <summary>
    /// Members are never deleted — registrations and attendance hang off them.
    /// Leaving the organization is a status change.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var member = await _db.Members.FindAsync(id);
        if (member is null)
        {
            return NotFound();
        }

        member.Status = member.Status == MembershipStatus.Active
            ? MembershipStatus.Inactive
            : MembershipStatus.Active;

        AuditTrail.Record(_db, _users.GetUserId(User), "MemberStatusChanged", nameof(Member), member.Id,
            $"{member.ListName} set to {member.Status}.");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{member.FullName} is now {member.Status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task GuardDuplicateStudentNumberAsync(MemberFormViewModel model)
    {
        var number = model.StudentNumber.Trim();

        var taken = await _db.Members
            .AnyAsync(m => m.StudentNumber == number && m.Id != model.Id);

        if (taken)
        {
            ModelState.AddModelError(nameof(model.StudentNumber),
                "Another member already has that student number.");
        }
    }
}
