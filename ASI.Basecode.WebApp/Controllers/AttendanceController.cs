using System.IO;
using System.Linq;
using System.Text;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// The door: records time-in and time-out against an event. The attendance
    /// pad lives on the event page, so every action returns there.
    /// </summary>
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class AttendanceController : BaseController
    {
        private readonly IAttendanceService _attendance;

        public AttendanceController(IAttendanceService attendance)
        {
            _attendance = attendance;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Record(AttendanceEntryViewModel entry)
        {
            // Marks the message as a door result, shown beside the scanner.
            TempData["Door"] = true;

            if (!ModelState.IsValid)
            {
                TempData["Error"] = Errors.StudentNumberRequired;
                return BackToEvent(entry.EventId);
            }

            try
            {
                var name = _attendance.Record(entry, CurrentUserName, out var recorded);
                TempData["Success"] = string.Format(
                    recorded == AttendanceAction.TimeOut ? Common.TimedOut : Common.TimedIn, name);
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return BackToEvent(entry.EventId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id, int eventId)
        {
            try
            {
                _attendance.DeleteAttendance(id);
                TempData["Success"] = Common.AttendanceDeleted;
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return BackToEvent(eventId);
        }

        public IActionResult Export(int eventId)
        {
            try
            {
                var csv = _attendance.ExportEventCsv(eventId, out var fileName);
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
                return File(bytes, "text/csv", fileName);
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        private IActionResult BackToEvent(int eventId) =>
            RedirectToAction("Details", "Events", new { id = eventId });
    }
}
