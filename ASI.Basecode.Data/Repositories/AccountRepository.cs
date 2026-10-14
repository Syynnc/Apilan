using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public class AccountRepository : BaseRepository, IAccountRepository
    {
        public AccountRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Account> GetAccounts()
        {
            return this.GetDbSet<Account>().Include(a => a.Role).Include(a => a.User);
        }

        public IQueryable<Role> GetRoles()
        {
            return this.GetDbSet<Role>();
        }

        public void AddAccount(string username, string passwordHash, int roleId, int? userId, string createdBy)
        {
            Execute($@"EXEC dbo.sp_Account_Create @username = {username}, @password_hash = {passwordHash},
                @role_id = {roleId}, @user_id = {userId}, @created_by = {createdBy}");
        }

        public void ChangePassword(int id, string passwordHash, string updatedBy)
        {
            Execute($"EXEC dbo.sp_Account_ChangePassword @id = {id}, @new_password_hash = {passwordHash}, @updated_by = {updatedBy}");
        }

        public void SetActive(int id, bool isActive, string updatedBy)
        {
            Execute($"EXEC dbo.sp_Account_SetActive @id = {id}, @is_active = {isActive}, @updated_by = {updatedBy}");
        }

        public void SetRole(int id, int roleId, string updatedBy)
        {
            Execute($"EXEC dbo.sp_Account_SetRole @id = {id}, @role_id = {roleId}, @updated_by = {updatedBy}");
        }

        public void RecordLogin(int id)
        {
            Execute($"EXEC dbo.sp_Account_RecordLogin @id = {id}");
        }
    }
}
