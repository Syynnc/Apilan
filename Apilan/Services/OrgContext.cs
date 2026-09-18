using System.Security.Claims;
using Apilan.Data;
using Apilan.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Services;

/// <summary>
/// Answers "which organization are we talking about" for the screens that need
/// one name rather than a list: the console shell's masthead and the org line
/// printed on a registration stub.
///
/// The rule, in order: the signed-in user's own org, then the only org on file
/// if there happens to be just one, then the product name. That last fallback
/// matters on a fresh install, where a stub still has to print something.
/// </summary>
public class OrgContext
{
    private const string Fallback = "Apilan";

    private readonly ApilanDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public OrgContext(ApilanDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>The organization the user acts for, or null for a campus-wide admin.</summary>
    public async Task<Organization?> CurrentAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return await OnlyOrgAsync();
        }

        var user = await _users.GetUserAsync(principal);

        if (user?.OrganizationId is null)
        {
            return await OnlyOrgAsync();
        }

        return await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);
    }

    /// <summary>The name to print, never null.</summary>
    public async Task<string> NameAsync(ClaimsPrincipal principal) =>
        (await CurrentAsync(principal))?.Name ?? Fallback;

    /// <summary>
    /// The org name for a specific event — a stub belongs to the org that ran
    /// the event, not to whoever happens to be looking at it.
    /// </summary>
    public async Task<string> NameForEventAsync(int eventId) =>
        await _db.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => e.Organization!.Name)
            .FirstOrDefaultAsync() ?? Fallback;

    /// <summary>
    /// The single org, when the install only has one. A one-org deployment is
    /// the common case for a student council, and it should not have to pick
    /// itself out of a list of one.
    /// </summary>
    private async Task<Organization?> OnlyOrgAsync()
    {
        var active = await _db.Organizations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .Take(2)
            .ToListAsync();

        return active.Count == 1 ? active[0] : null;
    }
}
