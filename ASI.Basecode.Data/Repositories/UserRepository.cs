using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<User> GetUsers()
        {
            return this.GetDbSet<User>();
        }

        public void AddUser(User user, string createdBy)
        {
            Execute($@"EXEC dbo.sp_User_Create
                @student_number = {user.StudentNumber}, @first_name = {user.FirstName}, @last_name = {user.LastName},
                @gender = {user.Gender}, @email = {user.Email}, @course = {user.Course},
                @year_level = {user.YearLevel}, @contact_number = {user.ContactNumber}, @created_by = {createdBy}");
        }

        public void UpdateUser(User user, string updatedBy)
        {
            Execute($@"EXEC dbo.sp_User_Update @id = {user.Id},
                @student_number = {user.StudentNumber}, @first_name = {user.FirstName}, @last_name = {user.LastName},
                @gender = {user.Gender}, @email = {user.Email}, @course = {user.Course},
                @year_level = {user.YearLevel}, @contact_number = {user.ContactNumber}, @updated_by = {updatedBy}");
        }

        public void SetActive(int id, bool isActive, string updatedBy)
        {
            Execute($"EXEC dbo.sp_User_SetActive @id = {id}, @is_active = {isActive}, @updated_by = {updatedBy}");
        }
    }
}
