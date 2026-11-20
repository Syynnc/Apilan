namespace ASI.Basecode.Resources.Constants
{
    // Class for enumerated values
    public class Enums
    {
        // Login Result
        public enum LoginResult
        {
            Success = 0,
            Failed = 1,
            Inactive = 2,

            // The account is enabled, but the student record it belongs to was deactivated.
            MemberInactive = 3,
        }

        // Which way an attendance entry is going
        public enum AttendanceAction
        {
            TimeIn = 0,
            TimeOut = 1,
        }
    }
}
