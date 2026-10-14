using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Shared helpers for reading the signed-in account out of the cookie.
    /// The username is what the stored procedures record as created_by /
    /// updated_by, so a change is always stamped with who made it.
    /// </summary>
    public abstract class BaseController : Controller
    {
        protected int AccountId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        protected string CurrentUserName =>
            User.Identity?.Name ?? "system";

        protected string DisplayName =>
            User.FindFirstValue(Const.ClaimDisplayName) ?? CurrentUserName;

        /// <summary>The linked student's id for a member account, if any.</summary>
        protected int? LinkedUserId =>
            int.TryParse(User.FindFirstValue(Const.ClaimUserId), out var id) ? id : (int?)null;

        protected bool IsOfficer => User.IsInRole(Const.RoleOfficer);

        /// <summary>
        /// True when the request came from the modal script rather than a full
        /// page load. The add/edit forms open in a modal, but every one of them
        /// still works as a plain page for a direct link or without JavaScript.
        /// </summary>
        protected bool IsModalRequest =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        /// <summary>The form on its own for the modal, or wrapped in its page otherwise.</summary>
        protected IActionResult FormView(object model, bool invalid = false)
        {
            if (!IsModalRequest)
            {
                return View("Form", model);
            }

            // 422 tells the modal to swap in the re-rendered form with its errors.
            if (invalid)
            {
                Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            }
            return PartialView("_Form", model);
        }

        /// <summary>After a successful save: a redirect for a page, JSON naming it for the modal.</summary>
        protected IActionResult SavedRedirect(string url)
        {
            return IsModalRequest ? Json(new { redirect = url }) : Redirect(url);
        }
    }
}
