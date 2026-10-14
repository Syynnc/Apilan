using System;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// When an event's door records what. Time-in opens by itself at the start:
    /// on time for the event's window (15 to 30 minutes), late after it, until
    /// the event ends. Time-out opens by itself at the end, for the window.
    /// The same rule is enforced again in sp_Attendance_TimeIn/TimeOut; this is
    /// what the door page and the service read to know where they stand.
    /// </summary>
    public sealed class AttendanceWindow
    {
        public enum Phase
        {
            /// <summary>Before the start: nothing to record yet.</summary>
            NotOpen,
            /// <summary>Inside the window: arrivals are on time.</summary>
            TimeIn,
            /// <summary>After the window, before the end: arrivals are late.</summary>
            LateTimeIn,
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
        /// <summary>When on-time arrivals end; time-in stays open, late, until the end.</summary>
        public DateTime TimeInCloses { get; }
        public DateTime TimeOutOpens { get; }
        public DateTime TimeOutCloses { get; }

        public Phase At(DateTime now) =>
            now < TimeInOpens ? Phase.NotOpen
            : now <= TimeInCloses ? Phase.TimeIn
            : now < TimeOutOpens ? Phase.LateTimeIn
            : now <= TimeOutCloses ? Phase.TimeOut
            : Phase.Closed;

        /// <summary>The next moment the phase changes, or null once it is closed.</summary>
        public DateTime? NextChange(DateTime now) =>
            At(now) switch
            {
                Phase.NotOpen => TimeInOpens,
                Phase.TimeIn => TimeInCloses.AddSeconds(1),
                Phase.LateTimeIn => TimeOutOpens,
                Phase.TimeOut => TimeOutCloses.AddSeconds(1),
                _ => null,
            };
    }
}
