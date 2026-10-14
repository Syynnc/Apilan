using System.Collections.Generic;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountService
    {
        LoginResult AuthenticateUser(string username, string password, out SignedInAccount account);
        void ChangePassword(int accountId, ChangePasswordViewModel model, string updatedBy);
        AccountsPageViewModel GetAccountsPage();
        void AddAccount(AccountViewModel model, string createdBy);
        string SetActive(int id, bool isActive, int currentAccountId, string updatedBy);
        string ResetPassword(int id, string newPassword, string updatedBy);
    }

    public interface IStudentService
    {
        StudentListViewModel GetStudents(string search, bool showInactive);
        StudentDetailViewModel GetStudentDetail(int id);
        StudentViewModel GetStudent(int id);
        void AddStudent(StudentViewModel model, string createdBy);
        void UpdateStudent(StudentViewModel model, string updatedBy);
        string SetActive(int id, bool isActive, string updatedBy);
        List<LookupItem> GetStudentLookup();

        /// <summary>The closest few members to a search term, for the live search box.</summary>
        List<StudentListItem> Suggest(string term, int take = 6);
    }

    public interface IEventService
    {
        EventListViewModel GetEvents(string status, string search);

        /// <summary>The closest few events by name or venue, for the live search box.</summary>
        List<EventSummary> Suggest(string term, int take = 6);
        EventDetailViewModel GetEventDetail(int id);
        EventViewModel GetEvent(int id);
        EventViewModel NewEvent();
        void PopulateLookups(EventViewModel model);
        void AddEvent(EventViewModel model, string createdBy);
        void UpdateEvent(EventViewModel model, string updatedBy);
        void DeleteEvent(int id);
    }

    public interface IEventTypeService
    {
        List<EventTypeViewModel> GetEventTypes();
        EventTypeViewModel GetEventType(int id);
        void SaveEventType(EventTypeViewModel model);
    }

    public interface IAttendanceService
    {
        /// <summary>
        /// Records a time-in or time-out and returns the student's name, with
        /// which of the two actually happened (an Auto entry resolves to one).
        /// </summary>
        string Record(AttendanceEntryViewModel entry, string recordedBy, out AttendanceAction recorded);
        void DeleteAttendance(int id);
        string ExportEventCsv(int eventId, out string fileName);
    }

    public interface IReportService
    {
        DashboardViewModel GetDashboard();
        ReportViewModel GetReport(string course);
    }
}
