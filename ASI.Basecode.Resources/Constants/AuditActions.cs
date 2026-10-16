using System.Collections.Generic;

namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// The action codes stored in dbo.audit_logs.action, with the words the
    /// activity log shows for them and the group they are filtered by.
    /// </summary>
    public static class AuditActions
    {
        public const string SignedIn = "SignedIn";
        public const string SignedOut = "SignedOut";
        public const string SignInFailed = "SignInFailed";
        public const string PasswordChanged = "PasswordChanged";

        public const string MemberCreated = "MemberCreated";
        public const string MemberUpdated = "MemberUpdated";
        public const string MemberDeactivated = "MemberDeactivated";
        public const string MemberRestored = "MemberRestored";

        public const string AccountCreated = "AccountCreated";
        public const string AccountDisabled = "AccountDisabled";
        public const string AccountEnabled = "AccountEnabled";
        public const string PasswordReset = "PasswordReset";
        public const string MadeOfficer = "MadeOfficer";
        public const string MadeMember = "MadeMember";

        public const string EventCreated = "EventCreated";
        public const string EventUpdated = "EventUpdated";
        public const string EventDeleted = "EventDeleted";
        public const string EventTypeCreated = "EventTypeCreated";
        public const string EventTypeUpdated = "EventTypeUpdated";

        public const string AttendanceRemoved = "AttendanceRemoved";

        /// <summary>Target types.</summary>
        public const string TargetMember = "Member";
        public const string TargetAccount = "Account";
        public const string TargetEvent = "Event";
        public const string TargetEventType = "EventType";
        public const string TargetAttendance = "Attendance";
        public const string TargetSession = "Session";

        public sealed record Info(string Label, string Group, string Tone);

        /// <summary>Label, filter group and colour tone for each action.</summary>
        public static readonly IReadOnlyDictionary<string, Info> All = new Dictionary<string, Info>
        {
            [SignedIn] = new("Signed in", "Sign-ins", "idle"),
            [SignedOut] = new("Signed out", "Sign-ins", "idle"),
            [SignInFailed] = new("Failed sign-in", "Sign-ins", "risk"),
            [PasswordChanged] = new("Changed own password", "Accounts", "idle"),

            [MemberCreated] = new("Added member", "Members", "ok"),
            [MemberUpdated] = new("Edited member", "Members", "info"),
            [MemberDeactivated] = new("Deactivated member", "Members", "risk"),
            [MemberRestored] = new("Restored member", "Members", "ok"),

            [AccountCreated] = new("Gave sign-in access", "Accounts", "ok"),
            [AccountDisabled] = new("Disabled account", "Accounts", "risk"),
            [AccountEnabled] = new("Enabled account", "Accounts", "ok"),
            [PasswordReset] = new("Reset password", "Accounts", "warn"),
            [MadeOfficer] = new("Made officer", "Accounts", "warn"),
            [MadeMember] = new("Made member", "Accounts", "warn"),

            [EventCreated] = new("Created event", "Events", "ok"),
            [EventUpdated] = new("Edited event", "Events", "info"),
            [EventDeleted] = new("Deleted event", "Events", "risk"),
            [EventTypeCreated] = new("Added event type", "Events", "ok"),
            [EventTypeUpdated] = new("Edited event type", "Events", "info"),

            [AttendanceRemoved] = new("Removed attendance", "Attendance", "risk"),
        };

        public static readonly IReadOnlyList<string> Groups = new[] { "Members", "Accounts", "Events", "Attendance" };

        /// <summary>Still recorded, but kept out of the audit log.</summary>
        public static readonly IReadOnlyList<string> Hidden = new[] { SignedIn, SignedOut, SignInFailed };

        public static Info Describe(string action) =>
            action != null && All.TryGetValue(action, out var info) ? info : new Info(action, "Other", "idle");
    }
}
