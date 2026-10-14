using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        /* ----------------------------- Sign in ----------------------------- */

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = _accountService.AuthenticateUser(model.Identifier, model.Password, out var account);

            if (result == LoginResult.Inactive)
            {
                ModelState.AddModelError(string.Empty, Errors.AccountInactive);
                return View(model);
            }

            if (result != LoginResult.Success)
            {
                ModelState.AddModelError(string.Empty, Errors.InvalidLogin);
                return View(model);
            }

            await SignInAsync(account);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(Const.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult Denied() => View();

        /* ------------------------- Change password -------------------------- */

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _accountService.ChangePassword(AccountId, model, CurrentUserName);
                TempData["Success"] = Common.PasswordChanged;
                return RedirectToAction(nameof(ChangePassword));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        /* -------------------- Account management (officer) ------------------ */

        [Authorize(Policy = Const.OfficerOnlyPolicy)]
        public IActionResult Index()
        {
            return View(_accountService.GetAccountsPage());
        }

        [Authorize(Policy = Const.OfficerOnlyPolicy)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var page = _accountService.GetAccountsPage();
                page.NewAccount = model;
                return View(nameof(Index), page);
            }

            try
            {
                _accountService.AddAccount(model, CurrentUserName);
                TempData["Success"] = string.Format(Common.AccountCreated, model.Username);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Const.OfficerOnlyPolicy)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetActive(int id, bool isActive)
        {
            try
            {
                var username = _accountService.SetActive(id, isActive, AccountId, CurrentUserName);
                TempData["Success"] = string.Format(Common.AccountStatusChanged, username, isActive ? "active" : "disabled");
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Const.OfficerOnlyPolicy)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "The new password must be at least 8 characters.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var username = _accountService.ResetPassword(id, newPassword, CurrentUserName);
                TempData["Success"] = string.Format(Common.PasswordReset, username);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        /* -------------------------------------------------------------------- */

        private async Task SignInAsync(SignedInAccount account)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new(ClaimTypes.Name, account.Username),
                new(ClaimTypes.Role, account.RoleName),
                new(Const.ClaimDisplayName, account.DisplayName ?? account.Username),
            };

            if (account.UserId.HasValue)
            {
                claims.Add(new Claim(Const.ClaimUserId, account.UserId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, Const.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            var properties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(Const.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
        }
    }
}
