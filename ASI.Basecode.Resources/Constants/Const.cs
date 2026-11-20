namespace ASI.Basecode.Resources.Constants
{
    // Class for variables with constant values
    public class Const
    {
        // Cookie authentication scheme name
        public const string AuthenticationScheme = "Apilan";

        // Claims issuer
        public const string Issuer = "apilan";

        // Role names, exactly as stored in dbo.roles
        public const string RoleOfficer = "Officer";
        public const string RoleMember = "Member";

        /* accounts.id of the initial officer, the bootstrap account that
           database/01_RolesAndAccounts.sql sets up. Other officers cannot
           disable it, reset its password or take its officer role away, so the
           organization can never be locked out of its own console. */
        public const int InitialOfficerAccountId = 1;

        // Authorization policy guarding the console: Officer role only
        public const string OfficerOnlyPolicy = "OfficerOnly";

        // Custom claim carrying the linked student's id, when the account has one
        public const string ClaimUserId = "UserId";

        // Custom claim carrying the linked student's display name
        public const string ClaimDisplayName = "DisplayName";

        /* Present while the account is still on the default password: every page
           then opens the change-password prompt before anything else. */
        public const string ClaimMustChangePassword = "MustChangePassword";

        // attendance_modes.id for a member pass read by the door camera
        public const int QrAttendanceMode = 1;

        // attendance_modes.id for an officer typing a student in by hand
        public const int ManualAttendanceMode = 2;

        // Event status values produced by vw_EventAttendanceSummary
        public const string StatusUpcoming = "Upcoming";
        public const string StatusOngoing = "Ongoing";
        public const string StatusFinished = "Finished";
    }
}
