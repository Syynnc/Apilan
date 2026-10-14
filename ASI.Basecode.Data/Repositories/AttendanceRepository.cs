using System;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    public class AttendanceRepository : BaseRepository, IAttendanceRepository
    {
        public AttendanceRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<AttendanceDetail> GetAttendanceDetails()
        {
            return this.GetDbSet<AttendanceDetail>();
        }

        public IQueryable<EventAbsentee> GetAbsentees()
        {
            return this.GetDbSet<EventAbsentee>();
        }

        public IQueryable<StudentSummary> GetStudentSummaries()
        {
            return this.GetDbSet<StudentSummary>();
        }

        public IQueryable<AttendanceMode> GetModes()
        {
            return this.GetDbSet<AttendanceMode>();
        }

        public void TimeIn(int eventId, string studentNumber, int attendanceModeId, DateTime? timeIn, string recordedBy)
        {
            Execute($@"EXEC dbo.sp_Attendance_TimeIn @event_id = {eventId}, @attendance_mode_id = {attendanceModeId},
                @student_number = {studentNumber}, @time_in = {timeIn}, @recorded_by = {recordedBy}");
        }

        public void TimeOut(int eventId, string studentNumber, DateTime? timeOut, string recordedBy)
        {
            Execute($@"EXEC dbo.sp_Attendance_TimeOut @event_id = {eventId},
                @student_number = {studentNumber}, @time_out = {timeOut}, @recorded_by = {recordedBy}");
        }

        public void DeleteAttendance(int id)
        {
            Execute($"EXEC dbo.sp_Attendance_Delete @id = {id}");
        }
    }
}
