using System;
using System.Linq;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IUserRepository
    {
        IQueryable<User> GetUsers();
        void AddUser(User user, string createdBy);
        void UpdateUser(User user, string updatedBy);
        void SetActive(int id, bool isActive, string updatedBy);
    }

    public interface IAccountRepository
    {
        IQueryable<Account> GetAccounts();
        IQueryable<Role> GetRoles();
        void AddAccount(string username, string passwordHash, int roleId, int? userId, string createdBy);
        void ChangePassword(int id, string passwordHash, string updatedBy);
        void SetActive(int id, bool isActive, string updatedBy);
        void SetRole(int id, int roleId, string updatedBy);
        void RecordLogin(int id);
    }

    public interface IEventRepository
    {
        IQueryable<Event> GetEvents();
        IQueryable<EventSummary> GetEventSummaries();
        void AddEvent(Event evt, string createdBy);
        void UpdateEvent(Event evt, string updatedBy);
        void DeleteEvent(int id);
    }

    public interface IEventTypeRepository
    {
        IQueryable<EventType> GetEventTypes();
        void AddEventType(EventType eventType);
        void UpdateEventType(EventType eventType);
    }

    public interface IAttendanceRepository
    {
        IQueryable<AttendanceDetail> GetAttendanceDetails();
        IQueryable<EventAbsentee> GetAbsentees();
        IQueryable<StudentSummary> GetStudentSummaries();
        IQueryable<AttendanceMode> GetModes();
        void TimeIn(int eventId, string studentNumber, int attendanceModeId, DateTime? timeIn, string recordedBy);
        void TimeOut(int eventId, string studentNumber, DateTime? timeOut, string recordedBy);
        void DeleteAttendance(int id);
    }
}
