using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IStudentService _students;

        public HomeController(IStudentService students)
        {
            _students = students;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View();
            }

            // Officers land on the console; members on their own attendance.
            return IsOfficer
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction(nameof(MyAttendance));
        }

        /// <summary>A member's own record — their linked student's attendance history.</summary>
        [Authorize]
        public IActionResult MyAttendance()
        {
            if (IsOfficer)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (LinkedUserId is not int userId)
            {
                // A member account with no student record linked yet.
                return View("NoRecord");
            }

            return View(_students.GetStudentDetail(userId));
        }

        /// <summary>The signed-in member's own pass as a PNG, to keep on their phone.</summary>
        [Authorize]
        public IActionResult MyPass()
        {
            if (LinkedUserId is not int userId)
            {
                return NotFound();
            }

            var student = _students.GetStudent(userId);
            return File(QrCode.Png(student.StudentNumber), "image/png", $"apilan-pass-{student.StudentNumber}.png");
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
