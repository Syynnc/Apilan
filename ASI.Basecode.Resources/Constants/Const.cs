namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Class for variables with constant values
    /// </summary>
    public class Const
    {
        /// <summary>Cookie authentication scheme name</summary>
        public const string AuthenticationScheme = "Apilan";

        /// <summary>Claims issuer</summary>
        public const string Issuer = "apilan";

        /// <summary>Role names, exactly as stored in dbo.roles</summary>
        public const string RoleOfficer = "Officer";
        public const string RoleMember = "Member";

        /// <summary>Authorization policy guarding the console: Officer role only</summary>
        public const string OfficerOnlyPolicy = "OfficerOnly";

        /// <summary>Custom claim carrying the linked student's id, when the account has one</summary>
        public const string ClaimUserId = "UserId";

        /// <summary>Custom claim carrying the linked student's display name</summary>
        public const string ClaimDisplayName = "DisplayName";

        /// <summary>
        /// Present while the account is still on the default password: every page
        /// then opens the change-password prompt before anything else.
        /// </summary>
        public const string ClaimMustChangePassword = "MustChangePassword";

        /// <summary>attendance_modes.id for a member pass read by the door camera</summary>
        public const int QrAttendanceMode = 1;

        /// <summary>attendance_modes.id for an officer typing a student in by hand</summary>
        public const int ManualAttendanceMode = 2;

        /// <summary>Event status values produced by vw_EventAttendanceSummary</summary>
        public const string StatusUpcoming = "Upcoming";
        public const string StatusOngoing = "Ongoing";
        public const string StatusFinished = "Finished";
    }
}
