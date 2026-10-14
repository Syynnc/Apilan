using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class AttendanceService : ServiceBase, IAttendanceService
    {
        private readonly IAttendanceRepository _attendance;
        private readonly IUserRepository _users;
        private readonly IEventRepository _events;

        public AttendanceService(IAttendanceRepository attendance, IUserRepository users, IEventRepository events)
        {
            _attendance = attendance;
            _users = users;
            _events = events;
        }

        public string Record(AttendanceEntryViewModel entry, string recordedBy, out AttendanceAction recorded)
        {
            var studentNumber = entry.StudentNumber.Trim();

            // The clock decides which way a scan goes: time-in opens by itself at
            // the start and time-out at the end, each for the event's window.
            var evt = _events.GetEvents()
                .Where(e => e.Id == entry.EventId)
                .Select(e => new { e.EventStart, e.EventEnd, e.AttendanceWindowMinutes })
                .FirstOrDefault() ?? throw NotFound();
            var window = new AttendanceWindow(evt.EventStart, evt.EventEnd, evt.AttendanceWindowMinutes);

            switch (window.At(entry.RecordedAt ?? DateTime.Now))
            {
                case AttendanceWindow.Phase.TimeIn:
                    recorded = AttendanceAction.TimeIn;
                    break;
                case AttendanceWindow.Phase.TimeOut:
                    recorded = AttendanceAction.TimeOut;
                    break;
                case AttendanceWindow.Phase.NotOpen:
                    throw new InvalidDataException(string.Format(Errors.DoorNotOpen, window.TimeInOpens.ToString("h:mm tt")));
                case AttendanceWindow.Phase.Between:
                    throw new InvalidDataException(string.Format(Errors.DoorBetweenWindows,
                        window.TimeInCloses.ToString("h:mm tt"), window.TimeOutOpens.ToString("h:mm tt")));
                default:
                    throw new InvalidDataException(string.Format(Errors.DoorClosed, window.TimeOutCloses.ToString("h:mm tt")));
            }

            // One visit per event: someone already in and out has nothing left.
            if (recorded == AttendanceAction.TimeOut)
            {
                var timedOut = _attendance.GetAttendanceDetails()
                    .Any(a => a.EventId == entry.EventId && a.StudentNumber == studentNumber && a.TimeOut != null);
                if (timedOut)
                {
                    throw new InvalidDataException(string.Format(Errors.AlreadyTimedInAndOut, StudentName(studentNumber)));
                }
            }

            // The procedures check the windows again, duplicates and capacity.
            if (recorded == AttendanceAction.TimeOut)
            {
                Run(() => _attendance.TimeOut(entry.EventId, studentNumber, entry.RecordedAt, recordedBy));
            }
            else
            {
                Run(() => _attendance.TimeIn(entry.EventId, studentNumber, entry.AttendanceModeId, entry.RecordedAt, recordedBy));
            }

            return StudentName(studentNumber);
        }

        private string StudentName(string studentNumber)
        {
            return _users.GetUsers()
                .Where(u => u.StudentNumber == studentNumber)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefault() ?? studentNumber;
        }

        public void DeleteAttendance(int id)
        {
            Run(() => _attendance.DeleteAttendance(id));
        }

        public string ExportEventCsv(int eventId, out string fileName)
        {
            var evt = _events.GetEvents().FirstOrDefault(e => e.Id == eventId) ?? throw NotFound();
            var rows = _attendance.GetAttendanceDetails()
                .Where(a => a.EventId == eventId)
                .OrderBy(a => a.FullName)
                .ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Student number,Name,Course,Year,Mode,Time in,Time out,Minutes attended,Late");
            foreach (var r in rows)
            {
                csv.AppendLine(string.Join(",",
                    Cell(r.StudentNumber), Cell(r.FullName), Cell(r.Course), r.YearLevel?.ToString(),
                    Cell(r.AttendanceMode),
                    r.TimeIn.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    r.TimeOut?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    r.MinutesAttended?.ToString(),
                    r.IsLate == 1 ? "Yes" : "No"));
            }

            var safeName = new string(evt.EventName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
            fileName = $"attendance-{evt.Id}-{safeName}.csv";
            return csv.ToString();
        }

        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Quote, and defuse anything a spreadsheet would run as a formula.
            if ("=+-@".IndexOf(value[0]) >= 0)
            {
                value = "'" + value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
