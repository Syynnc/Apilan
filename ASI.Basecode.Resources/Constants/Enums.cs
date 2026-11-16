namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Class for enumerated values
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// Login Result
        /// </summary>
        public enum LoginResult
        {
            Success = 0,
            Failed = 1,
            Inactive = 2,

            /// <summary>The account is enabled, but the student record it belongs to was deactivated.</summary>
            MemberInactive = 3,
        }

        /// <summary>
        /// Which way an attendance entry is going
        /// </summary>
        public enum AttendanceAction
        {
            TimeIn = 0,
            TimeOut = 1,
        }
    }
}
