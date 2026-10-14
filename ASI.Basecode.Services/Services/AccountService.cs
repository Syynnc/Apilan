using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using System.IO;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : ServiceBase, IAccountService
    {
        private readonly IAccountRepository _accounts;
        private readonly IUserRepository _users;

        public AccountService(IAccountRepository accounts, IUserRepository users)
        {
            _accounts = accounts;
            _users = users;
        }

        public LoginResult AuthenticateUser(string username, string password, out SignedInAccount account)
        {
            account = null;

            var identifier = (username ?? string.Empty).Trim();

            // Sign in with the username, or — for a member — the linked student's
            // student number or email. The database keeps all three unique.
            var found = _accounts.GetAccounts().FirstOrDefault(a =>
                a.Username == identifier
                || (a.User != null && (a.User.StudentNumber == identifier || a.User.Email == identifier)));

            if (found == null || !PasswordManager.VerifyPassword(password, found.PasswordHash))
            {
                return LoginResult.Failed;
            }

            if (!found.IsActive)
            {
                return LoginResult.Inactive;
            }

            // Deactivating a member is meant to shut them out, but it only flips
            // dbo.users — the account row stays enabled — so check both.
            if (found.User != null && !found.User.IsActive)
            {
                return LoginResult.MemberInactive;
            }

            _accounts.RecordLogin(found.Id);

            account = new SignedInAccount
            {
                Id = found.Id,
                Username = found.Username,
                RoleName = found.Role.RoleName,
                DisplayName = found.User != null ? $"{found.User.FirstName} {found.User.LastName}" : found.Username,
                UserId = found.UserId,
            };
            return LoginResult.Success;
        }

        public void ChangePassword(int accountId, ChangePasswordViewModel model, string updatedBy)
        {
            var account = _accounts.GetAccounts().FirstOrDefault(a => a.Id == accountId) ?? throw NotFound();

            if (!PasswordManager.VerifyPassword(model.CurrentPassword, account.PasswordHash))
            {
                throw new InvalidDataException(Errors.CurrentPasswordWrong);
            }

            Run(() => _accounts.ChangePassword(accountId, PasswordManager.HashPassword(model.NewPassword), updatedBy));
        }

        public AccountsPageViewModel GetAccountsPage()
        {
            return new AccountsPageViewModel
            {
                Accounts = _accounts.GetAccounts()
                    .OrderBy(a => a.Username)
                    .Select(a => new AccountListItem
                    {
                        Id = a.Id,
                        Username = a.Username,
                        RoleName = a.Role.RoleName,
                        StudentName = a.User != null ? a.User.LastName + ", " + a.User.FirstName : null,
                        IsActive = a.IsActive,
                        LastLogin = a.LastLogin,
                    })
                    .ToList(),
                Roles = _accounts.GetRoles()
                    .OrderBy(r => r.Id)
                    .Select(r => new LookupItem(r.Id, r.RoleName))
                    .ToList(),
                // Only students without an account yet can be linked.
                Students = _users.GetUsers()
                    .Where(u => u.IsActive && u.Account == null)
                    .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                    .Select(u => new LookupItem(u.Id, u.LastName + ", " + u.FirstName + " (" + u.StudentNumber + ")"))
                    .ToList(),
            };
        }

        public void AddAccount(AccountViewModel model, string createdBy)
        {
            var hash = PasswordManager.HashPassword(model.Password);
            Run(() => _accounts.AddAccount(model.Username.Trim(), hash, model.RoleId!.Value, model.UserId, createdBy));
        }

        public string SetActive(int id, bool isActive, int currentAccountId, string updatedBy)
        {
            if (!isActive && id == currentAccountId)
            {
                throw new InvalidDataException(Errors.CannotDeactivateSelf);
            }

            var username = UsernameOf(id);
            Run(() => _accounts.SetActive(id, isActive, updatedBy));
            return username;
        }

        public string ResetPassword(int id, string newPassword, string updatedBy)
        {
            var username = UsernameOf(id);
            Run(() => _accounts.ChangePassword(id, PasswordManager.HashPassword(newPassword), updatedBy));
            return username;
        }

        private string UsernameOf(int id)
        {
            return _accounts.GetAccounts().Where(a => a.Id == id).Select(a => a.Username).FirstOrDefault()
                   ?? throw NotFound();
        }
    }
}
