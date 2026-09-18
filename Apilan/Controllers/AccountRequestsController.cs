using Apilan.Data;
using Apilan.Models;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Account Registration Approval. Officers and admins review student account
/// requests; approving one activates the account, grants the Member role, and
/// activates the linked member record.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class AccountRequestsController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApilanDbContext _db;
    private readonly ILogger<AccountRequestsController> _log;

    public AccountRequestsController(
        UserManager<ApplicationUser> users,
        ApilanDbContext db,
        ILogger<AccountRequestsController> log)
    {
        _users = users;
        _db = db;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Index(AccountStatus? status)
    {
        // Default to the queue that needs action.
        var filter = status ?? AccountStatus.Pending;

        var query = _users.Users
            .Include(u => u.Member)
            .Include(u => u.ReviewedBy)
            .Where(u => u.Status == filter);

        var rows = await query
            .OrderByDescending(u => u.RequestedOn)
            .Select(u => new AccountRequestViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                StudentNumber = u.StudentNumber,
                Email = u.Email!,
                Course = u.Member!.Course,
                YearLevel = u.Member.YearLevel,
                Section = u.Member.Section,
                ContactNumber = u.Member.ContactNumber,
                RequestedOn = u.RequestedOn,
                Status = u.Status,
                ReviewedOn = u.ReviewedOn,
                ReviewNote = u.ReviewNote,
                ReviewedByName = u.ReviewedBy!.FullName
            })
            .ToListAsync();

        var model = new AccountRequestListViewModel
        {
            Requests = rows,
            Filter = filter,
            PendingCount = await _users.Users.CountAsync(u => u.Status == AccountStatus.Pending)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        var user = await _users.Users
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Status != AccountStatus.Pending)
        {
            TempData["Error"] = "That request has already been reviewed.";
            return RedirectToAction(nameof(Index));
        }

        user.Status = AccountStatus.Active;
        user.ReviewedOn = DateTime.Now;
        user.ReviewedByUserId = _users.GetUserId(User);
        user.ReviewNote = null;

        if (!await _users.IsInRoleAsync(user, Roles.Member))
        {
            await _users.AddToRoleAsync(user, Roles.Member);
        }

        if (user.Member is not null)
        {
            user.Member.Status = MembershipStatus.Active;
        }

        await _users.UpdateAsync(user);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _users.GetUserId(User),
            Action = "AccountApproved",
            EntityName = nameof(ApplicationUser),
            EntityId = user.Id,
            Detail = $"Approved {user.FullName} ({user.StudentNumber})."
        });
        await _db.SaveChangesAsync();

        _log.LogInformation("Account {Email} approved.", user.Email);

        TempData["Success"] = $"{user.FullName} can now sign in.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, string? note)
    {
        var user = await _users.Users
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Status != AccountStatus.Pending)
        {
            TempData["Error"] = "That request has already been reviewed.";
            return RedirectToAction(nameof(Index));
        }

        user.Status = AccountStatus.Rejected;
        user.ReviewedOn = DateTime.Now;
        user.ReviewedByUserId = _users.GetUserId(User);
        user.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await _users.UpdateAsync(user);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _users.GetUserId(User),
            Action = "AccountRejected",
            EntityName = nameof(ApplicationUser),
            EntityId = user.Id,
            Detail = $"Rejected {user.FullName} ({user.StudentNumber}). {user.ReviewNote}".Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{user.FullName}'s request was rejected.";
        return RedirectToAction(nameof(Index));
    }
}
