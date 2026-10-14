using System;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// When an event's door records what. Time-in opens by itself at the start
    /// and time-out at the end, each for the event's window (15 to 30 minutes).
    /// The same rule is enforced again in sp_Attendance_TimeIn/TimeOut; this is
    /// what the door page and the service read to know where they stand.
    /// </summary>
    public sealed class AttendanceWindow
    {
        public enum Phase
        {
            /// <summary>Before the start: nothing to record yet.</summary>
            NotOpen,
            TimeIn,
            /// <summary>After time-in closed, before the event ends.</summary>
            Between,
            TimeOut,
            /// <summary>Time-out has closed: attendance is final.</summary>
            Closed,
        }

        public AttendanceWindow(DateTime start, DateTime end, int minutes)
        {
            Minutes = minutes;
            TimeInOpens = start;
            TimeInCloses = start.AddMinutes(minutes);
            TimeOutOpens = end;
            TimeOutCloses = end.AddMinutes(minutes);
        }

        public int Minutes { get; }
        public DateTime TimeInOpens { get; }
        public DateTime TimeInCloses { get; }
        public DateTime TimeOutOpens { get; }
        public DateTime TimeOutCloses { get; }

        public Phase At(DateTime now) =>
            now < TimeInOpens ? Phase.NotOpen
            : now <= TimeInCloses ? Phase.TimeIn
            : now < TimeOutOpens ? Phase.Between
            : now <= TimeOutCloses ? Phase.TimeOut
            : Phase.Closed;

        /// <summary>The next moment the phase changes, or null once it is closed.</summary>
        public DateTime? NextChange(DateTime now) =>
            At(now) switch
            {
                Phase.NotOpen => TimeInOpens,
                Phase.TimeIn => TimeInCloses.AddSeconds(1),
                Phase.Between => TimeOutOpens,
                Phase.TimeOut => TimeOutCloses.AddSeconds(1),
                _ => null,
            };
    }
}
