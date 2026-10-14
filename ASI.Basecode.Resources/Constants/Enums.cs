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
        }

        /// <summary>
        /// Which way an attendance entry is going
        /// </summary>
        public enum AttendanceAction
        {
            TimeIn = 0,
            TimeOut = 1,

            /// <summary>Time in, or time out if the student is already inside — what a scan at the door means.</summary>
            Auto = 2,
        }
    }
}
