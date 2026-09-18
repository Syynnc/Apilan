using Apilan.Data;
using Apilan.Models;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Authentication module: user login, account request, and sign-out.
/// Approval of requests lives in <see cref="AccountRequestsController"/>,
/// since that is an officer task rather than a public one.
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ApilanDbContext _db;
    private readonly ILogger<AccountController> _log;

    public AccountController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        ApilanDbContext db,
        ILogger<AccountController> log)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _log = log;
    }

    // ---- Login ------------------------------------------------------------

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLanding();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await FindByIdentifierAsync(model.Identifier);

        // Same message whether the account is missing or the password is wrong,
        // so the form cannot be used to discover who has an account.
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "That email or student number and password do not match.");
            return View(model);
        }

        switch (user.Status)
        {
            case AccountStatus.Pending:
                ModelState.AddModelError(string.Empty,
                    "This account is still waiting for an officer to approve it.");
                return View(model);

            case AccountStatus.Rejected:
                ModelState.AddModelError(string.Empty,
                    "This account request was not approved. Contact an officer if you think that is a mistake.");
                return View(model);

            case AccountStatus.Disabled:
                ModelState.AddModelError(string.Empty,
                    "This account has been deactivated. Contact an administrator.");
                return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Too many failed attempts. Try again in a few minutes.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "That email or student number and password do not match.");
            return View(model);
        }

        _log.LogInformation("{User} signed in.", user.Email);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return await RedirectToRoleHomeAsync(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---- Account request --------------------------------------------------

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLanding();
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _users.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account already uses this email address.");
            return View(model);
        }

        if (await _users.Users.AnyAsync(u => u.StudentNumber == model.StudentNumber))
        {
            ModelState.AddModelError(nameof(model.StudentNumber),
                "An account request already exists for this student number.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = $"{model.FirstName.Trim()} {model.LastName.Trim()}",
            StudentNumber = model.StudentNumber.Trim(),
            Status = AccountStatus.Pending,
            RequestedOn = DateTime.Now
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // Park the applicant's details on a member record so the officer
        // reviewing the request can see who they are. It stays Inactive until
        // the request is approved.
        var existing = await _db.Members
            .FirstOrDefaultAsync(m => m.StudentNumber == user.StudentNumber);

        if (existing is null)
        {
            var member = new Member
            {
                StudentNumber = user.StudentNumber!,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Course = model.Course.Trim(),
                YearLevel = model.YearLevel,
                Section = model.Section?.Trim(),
                Email = model.Email,
                ContactNumber = model.ContactNumber?.Trim(),
                MembershipType = MembershipType.Regular,
                Status = MembershipStatus.Inactive,
                JoinedOn = DateTime.Now
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();

            user.MemberId = member.Id;
        }
        else
        {
            user.MemberId = existing.Id;
        }

        await _users.UpdateAsync(user);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AccountRequested",
            EntityName = nameof(ApplicationUser),
            EntityId = user.Id,
            Detail = $"{user.FullName} ({user.StudentNumber}) requested an account."
        });
        await _db.SaveChangesAsync();

        _log.LogInformation("Account requested by {Email}.", user.Email);

        TempData["RequestedEmail"] = user.Email;
        return RedirectToAction(nameof(Submitted));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Submitted()
    {
        ViewData["Email"] = TempData["RequestedEmail"] as string;
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denied() => View();

    // ---- Helpers ----------------------------------------------------------

    /// <summary>Resolves an email address or a student number to an account.</summary>
    private async Task<ApplicationUser?> FindByIdentifierAsync(string identifier)
    {
        var value = identifier.Trim();

        if (value.Contains('@'))
        {
            return await _users.FindByEmailAsync(value);
        }

        return await _users.Users
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.StudentNumber == value);
    }

    private async Task<IActionResult> RedirectToRoleHomeAsync(ApplicationUser user)
    {
        if (await _users.IsInRoleAsync(user, Roles.Officer))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return RedirectToAction("Index", "MyEvents");
    }

    private IActionResult RedirectToLanding() => RedirectToAction("Index", "Home");
}
