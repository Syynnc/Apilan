using System.IO;
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

        public MembersController(IStudentService students)
        {
            _students = students;
        }

        public IActionResult Index(string search, bool showInactive = false)
        {
            return View(_students.GetStudents(search, showInactive));
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
        public IActionResult Create() => View("Form", new StudentViewModel());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return View("Form", _students.GetStudent(id));
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
                return View("Form", model);
            }

            try
            {
                if (model.Id == 0)
                {
                    _students.AddStudent(model, CurrentUserName);
                }
                else
                {
                    _students.UpdateStudent(model, CurrentUserName);
                }

                TempData["Success"] = string.Format(Common.StudentSaved, model.StudentNumber);
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Form", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetActive(int id, bool isActive)
        {
            try
            {
                var name = _students.SetActive(id, isActive, CurrentUserName);
                TempData["Success"] = string.Format(
                    isActive ? Common.StudentRestored : Common.StudentDeactivated, name);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
