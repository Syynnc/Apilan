using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Microsoft.AspNetCore.Http;
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

            if (result == LoginResult.MemberInactive)
            {
                ModelState.AddModelError(string.Empty, Errors.MemberDeactivated);
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
        public IActionResult ChangePassword() =>
            View(new ChangePasswordViewModel { FirstSignIn = MustChangePassword });

        /// <summary>
        /// Both the change-password page and the first-sign-in prompt post here;
        /// the prompt posts from the modal script and gets the form back (422)
        /// or JSON naming the page to carry on to.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!model.FirstSignIn && string.IsNullOrEmpty(model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Enter your current password.");
            }

            if (!ModelState.IsValid)
            {
                return PasswordView(model, invalid: true);
            }

            try
            {
                _accountService.ChangePassword(AccountId, model, CurrentUserName);
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return PasswordView(model, invalid: true);
            }

            // Re-issue the cookie without the must-change flag, so the prompt stops.
            if (MustChangePassword)
            {
                var claims = User.Claims.Where(c => c.Type != Const.ClaimMustChangePassword);
                await SignInAsync(new ClaimsIdentity(claims, Const.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));
            }

            TempData["Success"] = Common.PasswordChanged;
            var next = !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : Url.Action("Index", "Home");
            return SavedRedirect(next);
        }

        private IActionResult PasswordView(ChangePasswordViewModel model, bool invalid)
        {
            // Never send a typed password back down to the browser.
            ModelState.Remove(nameof(model.CurrentPassword));
            ModelState.Remove(nameof(model.NewPassword));
            ModelState.Remove(nameof(model.ConfirmPassword));
            model.CurrentPassword = model.NewPassword = model.ConfirmPassword = null;

            if (!IsModalRequest)
            {
                return View(nameof(ChangePassword), model);
            }

            if (invalid)
            {
                Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            }
            return PartialView("_PasswordForm", model);
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
        public IActionResult ResetPassword(int id)
        {
            // Back to the default, never to a password the officer chose: the
            // member is then made to pick their own at the next sign-in.
            try
            {
                var username = _accountService.ResetPassword(id, CurrentUserName);
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

            if (account.MustChangePassword)
            {
                claims.Add(new Claim(Const.ClaimMustChangePassword, "true"));
            }

            await SignInAsync(new ClaimsIdentity(claims, Const.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));
        }

        private async Task SignInAsync(ClaimsIdentity identity)
        {
            var properties = new AuthenticationProperties { IsPersistent = true };
            await HttpContext.SignInAsync(Const.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
        }
    }
}
