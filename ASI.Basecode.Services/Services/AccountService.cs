using System.IO;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Options;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : ServiceBase, IAccountService
    {
        private readonly IAccountRepository _accounts;
        private readonly IUserRepository _users;
        private readonly ApilanSettings _settings;

        public AccountService(IAccountRepository accounts, IUserRepository users, IOptions<ApilanSettings> settings)
        {
            _accounts = accounts;
            _users = users;
            _settings = settings.Value;
        }

        public LoginResult AuthenticateUser(string username, string password, out SignedInAccount account)
        {
            account = null;

            var identifier = (username ?? string.Empty).Trim();

            // Sign in with a student ID (the linked member's student number) or
            // a username. Email is not accepted. The database keeps both unique.
            var found = _accounts.GetAccounts().FirstOrDefault(a =>
                a.Username == identifier
                || (a.User != null && a.User.StudentNumber == identifier));

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
                // The password just verified, so comparing the plain text is enough —
                // no second bcrypt round needed to know it is still the default.
                MustChangePassword = password == _settings.DefaultPassword,
            };
            return LoginResult.Success;
        }

        public void ChangePassword(int accountId, ChangePasswordViewModel model, string updatedBy)
        {
            var account = _accounts.GetAccounts().FirstOrDefault(a => a.Id == accountId) ?? throw NotFound();

            // A first sign-in proves itself with the default password instead of
            // asking the member to type back the one they were just given.
            var current = model.FirstSignIn ? _settings.DefaultPassword : model.CurrentPassword;
            if (!PasswordManager.VerifyPassword(current, account.PasswordHash))
            {
                throw new InvalidDataException(Errors.CurrentPasswordWrong);
            }

            if (model.NewPassword == _settings.DefaultPassword)
            {
                throw new InvalidDataException(Errors.NewPasswordIsDefault);
            }

            Run(() => _accounts.ChangePassword(accountId, PasswordManager.HashPassword(model.NewPassword), updatedBy));
        }

        public AccountsPageViewModel GetAccountsPage()
        {
            return new AccountsPageViewModel
            {
                DefaultPassword = _settings.DefaultPassword,
                // Officers first, then members by name; disabled accounts stay in place, greyed.
                Accounts = _accounts.GetAccounts()
                    .OrderBy(a => a.Role.RoleName == Const.RoleOfficer ? 0 : 1)
                    .ThenBy(a => a.User != null ? a.User.LastName : a.Username)
                    .ThenBy(a => a.User != null ? a.User.FirstName : null)
                    .Select(a => new AccountListItem
                    {
                        Id = a.Id,
                        Username = a.Username,
                        RoleName = a.Role.RoleName,
                        UserId = a.UserId,
                        StudentName = a.User != null ? a.User.LastName + ", " + a.User.FirstName : null,
                        StudentNumber = a.User != null ? a.User.StudentNumber : null,
                        StudentActive = a.User == null || a.User.IsActive,
                        IsActive = a.IsActive,
                        LastLogin = a.LastLogin,
                        CreatedTime = a.CreatedTime,
                    })
                    .ToList(),
                MembersWithoutAccount = _users.GetUsers().Count(u => u.IsActive && u.Account == null),
            };
        }

        public string CreateMemberAccount(int userId, string createdBy)
        {
            var user = _users.GetUsers()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.StudentNumber, u.FirstName, u.LastName, HasAccount = u.Account != null })
                .FirstOrDefault() ?? throw NotFound();

            if (user.HasAccount)
            {
                throw new InvalidDataException(Errors.AccountAlreadyExists);
            }

            var roleId = _accounts.GetRoles()
                .Where(r => r.RoleName == Const.RoleMember)
                .Select(r => (int?)r.Id)
                .FirstOrDefault() ?? throw new InvalidDataException(Errors.MemberRoleMissing);

            // The student number is the username: it is what members already
            // sign in with, and dbo.users keeps it unique.
            var hash = PasswordManager.HashPassword(_settings.DefaultPassword);
            Run(() => _accounts.AddAccount(user.StudentNumber, hash, roleId, user.Id, createdBy));

            return $"{user.FirstName} {user.LastName}";
        }

        public string SetActive(int id, bool isActive, int currentAccountId, string updatedBy)
        {
            if (!isActive && id == currentAccountId)
            {
                throw new InvalidDataException(Errors.CannotDeactivateSelf);
            }
            GuardInitialOfficer(id, currentAccountId);

            var username = UsernameOf(id);
            Run(() => _accounts.SetActive(id, isActive, updatedBy));
            return username;
        }

        public string ResetPassword(int id, int currentAccountId, string updatedBy)
        {
            // Resetting it to the known default would hand the account to
            // whoever reset it, so the initial officer is covered here too.
            GuardInitialOfficer(id, currentAccountId);

            var username = UsernameOf(id);
            Run(() => _accounts.ChangePassword(id, PasswordManager.HashPassword(_settings.DefaultPassword), updatedBy));
            return username;
        }

        public string SetOfficer(int id, bool makeOfficer, int currentAccountId, string updatedBy)
        {
            GuardInitialOfficer(id, currentAccountId);

            var account = _accounts.GetAccounts()
                .Where(a => a.Id == id)
                .Select(a => new { a.Username, a.IsActive, a.UserId, a.Role.RoleName })
                .FirstOrDefault() ?? throw NotFound();

            var target = makeOfficer ? Const.RoleOfficer : Const.RoleMember;
            if (account.RoleName == target)
            {
                return account.Username;
            }

            if (makeOfficer && !account.IsActive)
            {
                throw new InvalidDataException(Errors.DisabledCannotBePromoted);
            }

            if (!makeOfficer)
            {
                // Nobody locks themselves out of the console by accident.
                if (id == currentAccountId)
                {
                    throw new InvalidDataException(Errors.CannotDemoteSelf);
                }

                // A member account without a member behind it has no pass and
                // no attendance page to land on.
                if (account.UserId == null)
                {
                    throw new InvalidDataException(Errors.OfficerHasNoMember);
                }
            }

            var roleId = _accounts.GetRoles()
                .Where(r => r.RoleName == target)
                .Select(r => (int?)r.Id)
                .FirstOrDefault()
                ?? throw new InvalidDataException(makeOfficer ? Errors.OfficerRoleMissing : Errors.MemberRoleMissing);

            Run(() => _accounts.SetRole(id, roleId, updatedBy));
            return account.Username;
        }

        public string CurrentRole(int accountId)
        {
            var state = _accounts.GetAccounts()
                .Where(a => a.Id == accountId)
                .Select(a => new { a.IsActive, MemberActive = a.User == null || a.User.IsActive, a.Role.RoleName })
                .FirstOrDefault();

            return state != null && state.IsActive && state.MemberActive ? state.RoleName : null;
        }

        /// <summary>Only the initial officer may change the initial officer's account.</summary>
        private static void GuardInitialOfficer(int id, int currentAccountId)
        {
            if (id == Const.InitialOfficerAccountId && currentAccountId != Const.InitialOfficerAccountId)
            {
                throw new InvalidDataException(Errors.InitialOfficerProtected);
            }
        }

        private string UsernameOf(int id)
        {
            return _accounts.GetAccounts().Where(a => a.Id == id).Select(a => a.Username).FirstOrDefault()
                   ?? throw NotFound();
        }
    }
}
