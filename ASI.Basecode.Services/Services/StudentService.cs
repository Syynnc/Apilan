using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;

namespace ASI.Basecode.Services.Services
{
    public class StudentService : ServiceBase, IStudentService
    {
        private readonly IUserRepository _users;
        private readonly IAttendanceRepository _attendance;
        private readonly IMapper _mapper;

        public StudentService(IUserRepository users, IAttendanceRepository attendance, IMapper mapper)
        {
            _users = users;
            _attendance = attendance;
            _mapper = mapper;
        }

        public StudentListViewModel GetStudents(string search, bool showInactive)
        {
            var query = _users.GetUsers();

            if (!showInactive)
            {
                query = query.Where(u => u.IsActive);
            }

            // Same search as dbo.sp_User_Get: name, student number, or course.
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => u.FirstName.Contains(term)
                                      || u.LastName.Contains(term)
                                      || (u.FirstName + " " + u.LastName).Contains(term)
                                      || u.StudentNumber.Contains(term)
                                      || u.Course.Contains(term));
            }

            return new StudentListViewModel
            {
                Search = search,
                ShowInactive = showInactive,
                Students = query
                    .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                    .Select(u => new StudentListItem
                    {
                        Id = u.Id,
                        StudentNumber = u.StudentNumber,
                        FullName = u.LastName + ", " + u.FirstName,
                        Course = u.Course,
                        YearLevel = u.YearLevel,
                        Email = u.Email,
                        IsActive = u.IsActive,
                        EventsAttended = u.Attendances.Count,
                    })
                    .ToList(),
            };
        }

        public List<StudentListItem> Suggest(string term, int take = 6)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<StudentListItem>();
            }

            term = term.Trim();

            return _users.GetUsers()
                .Where(u => u.FirstName.Contains(term)
                         || u.LastName.Contains(term)
                         || (u.FirstName + " " + u.LastName).Contains(term)
                         || u.StudentNumber.Contains(term)
                         || u.Course.Contains(term))
                .OrderBy(u => u.IsActive ? 0 : 1)
                .ThenBy(u => u.LastName.StartsWith(term) || u.FirstName.StartsWith(term) || u.StudentNumber.StartsWith(term) ? 0 : 1)
                .ThenBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Take(take)
                .Select(u => new StudentListItem
                {
                    Id = u.Id,
                    StudentNumber = u.StudentNumber,
                    FullName = u.LastName + ", " + u.FirstName,
                    Course = u.Course,
                    YearLevel = u.YearLevel,
                    Email = u.Email,
                    IsActive = u.IsActive,
                })
                .ToList();
        }

        public StudentDetailViewModel GetStudentDetail(int id)
        {
            var student = GetStudent(id);
            // Most recent first: the last thing they attended is the first row.
            var history = _attendance.GetAttendanceDetails()
                .Where(a => a.UserId == id)
                .OrderByDescending(a => a.TimeIn)
                .ToList();

            return new StudentDetailViewModel
            {
                Student = student,
                History = history,
                EventsAttended = history.Count,
                TimesLate = history.Count(a => a.IsLate == 1),
                MissingTimeOuts = history.Count(a => a.TimeOut == null),
            };
        }

        public StudentViewModel GetStudent(int id)
        {
            var user = _users.GetUsers().FirstOrDefault(u => u.Id == id) ?? throw NotFound();
            return _mapper.Map<StudentViewModel>(user);
        }

        public void AddStudent(StudentViewModel model, string createdBy)
        {
            var user = _mapper.Map<User>(model);
            Run(() => _users.AddUser(user, createdBy));
        }

        public void UpdateStudent(StudentViewModel model, string updatedBy)
        {
            var user = _mapper.Map<User>(model);
            Run(() => _users.UpdateUser(user, updatedBy));
        }

        public string SetActive(int id, bool isActive, string updatedBy)
        {
            var name = _users.GetUsers().Where(u => u.Id == id).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
                       ?? throw NotFound();
            Run(() => _users.SetActive(id, isActive, updatedBy));
            return name;
        }

        public List<LookupItem> GetStudentLookup()
        {
            return _users.GetUsers()
                .Where(u => u.IsActive)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new LookupItem(u.Id, u.LastName + ", " + u.FirstName))
                .ToList();
        }
    }
}
