using System.Security.Claims;
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
    }
}
