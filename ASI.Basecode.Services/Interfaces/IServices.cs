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

        /// <summary>
        /// Gives a member a sign-in: username = student number, password = the
        /// configured default. Returns the member's name.
        /// </summary>
        string CreateMemberAccount(int userId, string createdBy);

        string SetActive(int id, bool isActive, int currentAccountId, string updatedBy);

        /// <summary>Puts an account back on the default password. Returns the username.</summary>
        string ResetPassword(int id, int currentAccountId, string updatedBy);

        /// <summary>
        /// Makes an account an officer, or an officer a member again. Returns
        /// the username.
        /// </summary>
        string SetOfficer(int id, bool makeOfficer, int currentAccountId, string updatedBy);

        /// <summary>
        /// The role a signed-in account holds now, or null when it may no longer
        /// be signed in (disabled, deactivated or gone). Checked on every request
        /// so a role change or a disable takes effect at once.
        /// </summary>
        string CurrentRole(int accountId);
    }

    /// <summary>The activity log: who did what, and when.</summary>
    public interface IAuditService
    {
        /// <summary>Writes one entry. Never throws: a failure goes to the server log.</summary>
        void Record(AuditEntry entry);

        ActivityPageViewModel GetPage(ActivityFilter filter);
    }

    public interface IStudentService
    {
        StudentListViewModel GetStudents(string search, bool showInactive);
        StudentDetailViewModel GetStudentDetail(int id);
        StudentViewModel GetStudent(int id);
        /// <summary>Adds the member and their sign-in together. Returns the new member id.</summary>
        int AddStudent(StudentViewModel model, string createdBy);
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
        /// Records a time-in or time-out, whichever window is open, and returns
        /// the student's name with which of the two it was.
        /// </summary>
        string Record(AttendanceEntryViewModel entry, string recordedBy, out AttendanceAction recorded);
        void DeleteAttendance(int id);

        /// <summary>"Name (student no.) at Event", for the activity log; null if it is gone.</summary>
        string DescribeAttendance(int id);
        string ExportEventCsv(int eventId, out string fileName);
    }

    public interface IReportService
    {
        DashboardViewModel GetDashboard();
        ReportViewModel GetReport(ReportFilter filter);

        /// <summary>
        /// The same report as a CSV for Excel or Sheets: the filters, the
        /// headline figures, then every breakdown and table, one section each.
        /// </summary>
        string ExportCsv(ReportFilter filter, string generatedBy, out string fileName);
    }
}
