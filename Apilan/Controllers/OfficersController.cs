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
/// Administration · Officers and roles. Admin-only, because this is the screen
/// that decides who can run events. Two rules hold throughout: an admin cannot
/// strip or disable their own access, and the system never runs out of admins.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class OfficersController : Controller
{
    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<OfficersController> _log;

    public OfficersController(
        ApilanDbContext db,
        UserManager<ApplicationUser> users,
        ILogger<OfficersController> log)
    {
        _db = db;
        _users = users;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? role, string? search)
    {
        var query = _users.Users
            .Include(u => u.Member)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                u.Email!.Contains(term) ||
                (u.StudentNumber != null && u.StudentNumber.Contains(term)));
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        var me = _users.GetUserId(User);

        var rows = new List<AccountRowViewModel>();

        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            var held = roles.Contains(Roles.Officer) ? Roles.Officer
                : roles.Contains(Roles.Officer) ? Roles.Officer
                : Roles.Member;

            if (!string.IsNullOrWhiteSpace(role) && held != role)
            {
                continue;
            }

            rows.Add(new AccountRowViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                StudentNumber = user.StudentNumber,
                Status = user.Status,
                Role = held,
                MemberId = user.MemberId,
                MemberName = user.Member?.ListName,
                RequestedOn = user.RequestedOn,
                ReviewedOn = user.ReviewedOn,
                IsSelf = user.Id == me
            });
        }

        var linkedMemberIds = users.Where(u => u.MemberId.HasValue).Select(u => u.MemberId!.Value).ToList();

        return View(new OfficerListViewModel
        {
            Accounts = rows,
            Role = role,
            Search = search,
            Officers = await CountInRoleAsync(Roles.Officer),
            Members = await CountInRoleAsync(Roles.Member),
            UnlinkedMembers = await _db.Members
                .Where(m => m.Status == MembershipStatus.Active && !linkedMemberIds.Contains(m.Id))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .Select(m => new MemberOption(m.Id, m.LastName + ", " + m.FirstName, m.StudentNumber))
                .ToListAsync()
        });
    }

    /// <summary>
    /// Promotes or demotes an account. A user holds exactly one of the two
    /// roles, so the old one comes off as the new one goes on.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        if (!Roles.All.Contains(role))
        {
            TempData["Error"] = "That is not a role this system has.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == _users.GetUserId(User))
        {
            TempData["Error"] = "You cannot change your own role. Ask another officer.";
            return RedirectToAction(nameof(Index));
        }

        var current = await _users.GetRolesAsync(user);

        // With officers as the only staff role, demoting the last one would
        // leave the console with nobody able to open it — and nobody able to
        // promote a replacement. Refuse rather than lock everyone out.
        if (current.Contains(Roles.Officer) && role != Roles.Officer && await CountInRoleAsync(Roles.Officer) <= 1)
        {
            TempData["Error"] = "That is the last officer account. Promote someone else first.";
            return RedirectToAction(nameof(Index));
        }

        if (current.Any())
        {
            await _users.RemoveFromRolesAsync(user, current);
        }

        await _users.AddToRoleAsync(user, role);

        AuditTrail.Record(_db, _users.GetUserId(User), "RoleChanged", nameof(ApplicationUser), user.Id,
            $"{user.FullName}: {string.Join(", ", current)} → {role}.");
        await _db.SaveChangesAsync();

        _log.LogInformation("Role for {Email} changed to {Role}.", user.Email, role);

        var article = "aeiou".Contains(char.ToLowerInvariant(role[0])) ? "an" : "a";
        TempData["Success"] = $"{user.FullName} is now {article} {role.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Disabling keeps the account and its history but closes the door. It is
    /// the only "delete" this screen offers, on purpose.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAccess(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == _users.GetUserId(User))
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Index));
        }

        if (user.Status == AccountStatus.Active
            && await _users.IsInRoleAsync(user, Roles.Officer)
            && await CountInRoleAsync(Roles.Officer) <= 1)
        {
            TempData["Error"] = "That is the last officer account, so it cannot be disabled.";
            return RedirectToAction(nameof(Index));
        }

        user.Status = user.Status == AccountStatus.Active ? AccountStatus.Disabled : AccountStatus.Active;
        user.ReviewedOn = DateTime.Now;
        user.ReviewedByUserId = _users.GetUserId(User);

        await _users.UpdateAsync(user);

        // An account that has just been closed should not keep a live session.
        if (user.Status == AccountStatus.Disabled)
        {
            await _users.UpdateSecurityStampAsync(user);
        }

        AuditTrail.Record(_db, _users.GetUserId(User),
            user.Status == AccountStatus.Disabled ? "AccountDisabled" : "AccountEnabled",
            nameof(ApplicationUser), user.Id, user.FullName);
        await _db.SaveChangesAsync();

        TempData["Success"] = user.Status == AccountStatus.Disabled
            ? $"{user.FullName} can no longer sign in."
            : $"{user.FullName} can sign in again.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Points an account at its roster record. Without the link a member sees
    /// none of their own registrations, which is the most common support call.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkMember(string id, int memberId)
    {
        var user = await _users.FindByIdAsync(id);
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId);

        if (user is null || member is null)
        {
            return NotFound();
        }

        var taken = await _db.Users.AnyAsync(u => u.MemberId == memberId && u.Id != id);
        if (taken)
        {
            TempData["Error"] = $"{member.FullName} is already linked to another account.";
            return RedirectToAction(nameof(Index));
        }

        user.MemberId = member.Id;
        user.StudentNumber ??= member.StudentNumber;

        await _users.UpdateAsync(user);

        AuditTrail.Record(_db, _users.GetUserId(User), "AccountLinked", nameof(ApplicationUser), user.Id,
            $"{user.FullName} → member {member.ListName} ({member.StudentNumber}).");
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{user.FullName} is now linked to {member.FullName}'s record.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> CountInRoleAsync(string role) =>
        (await _users.GetUsersInRoleAsync(role)).Count;
}
