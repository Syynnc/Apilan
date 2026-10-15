using System.IO;
using System.Linq;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>Members are the students in dbo.users.</summary>
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class MembersController : BaseController
    {
        private readonly IStudentService _students;
        private readonly IAccountService _accounts;

        public MembersController(IStudentService students, IAccountService accounts)
        {
            _students = students;
            _accounts = accounts;
        }

        public IActionResult Index(string search, bool showInactive = false)
        {
            return View(_students.GetStudents(search, showInactive));
        }

        /// <summary>JSON for the live search box: the closest few members.</summary>
        [HttpGet]
        public IActionResult Suggest(string term)
        {
            var items = _students.Suggest(term).Select(m => new
            {
                url = Url.Action(nameof(Details), new { id = m.Id }),
                title = m.FullName,
                subtitle = string.Join(" · ", new[] { m.StudentNumber, m.Course, YearLevels.Label(m.YearLevel) }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                meta = m.IsActive ? null : "Deactivated",
                badge = Ui.Initials(m.FullName.Contains(',') ? m.FullName.Split(',')[1] + " " + m.FullName.Split(',')[0] : m.FullName),
                tone = m.IsActive ? "" : "off",
            });
            return Json(items);
        }

        public IActionResult Details(int id)
        {
            try
            {
                return View(_students.GetStudentDetail(id));
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        /// <summary>The member's attendance pass as a PNG, to print or send to them.</summary>
        public IActionResult Pass(int id)
        {
            try
            {
                var student = _students.GetStudent(id);
                return File(QrCode.Png(student.StudentNumber), "image/png", $"pass-{student.StudentNumber}.png");
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult Create() => FormView(new StudentViewModel());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return FormView(_students.GetStudent(id));
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(StudentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return FormView(model, invalid: true);
            }

            try
            {
                var label = $"{model.FirstName} {model.LastName} ({model.StudentNumber})";
                if (model.Id == 0)
                {
                    var id = _students.AddStudent(model, CurrentUserName);
                    Audit(AuditActions.MemberCreated, AuditActions.TargetMember, id, label,
                        $"With a sign-in: username {model.StudentNumber}, default password");
                    TempData["Success"] = string.Format(Common.StudentSaved, model.StudentNumber) + " "
                        + string.Format(Common.MemberAccountCreated, $"{model.FirstName} {model.LastName}");
                }
                else
                {
                    var before = _students.GetStudent(model.Id);
                    _students.UpdateStudent(model, CurrentUserName);
                    Audit(AuditActions.MemberUpdated, AuditActions.TargetMember, model.Id, label,
                        AuditDiff.Changes(before, model));
                    TempData["Success"] = string.Format(Common.StudentSaved, model.StudentNumber);
                }

                return SavedRedirect(Url.Action(nameof(Index)));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return FormView(model, invalid: true);
            }
        }

        /// <summary>
        /// Gives an existing member a sign-in. New members get one when they are
        /// created; this is for records that predate that.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAccount(int id)
        {
            try
            {
                var name = _accounts.CreateMemberAccount(id, CurrentUserName);
                Audit(AuditActions.AccountCreated, AuditActions.TargetMember, id, name, "Username is the student number, default password");
                TempData["Success"] = string.Format(Common.MemberAccountCreated, name);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetActive(int id, bool isActive, string returnUrl = null)
        {
            try
            {
                var name = _students.SetActive(id, isActive, CurrentUserName);
                Audit(isActive ? AuditActions.MemberRestored : AuditActions.MemberDeactivated,
                    AuditActions.TargetMember, id, name);
                TempData["Success"] = string.Format(
                    isActive ? Common.StudentRestored : Common.StudentDeactivated, name);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(nameof(Index));
        }
    }
}
