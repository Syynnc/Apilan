namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Year levels run 1 to 5. A non-blocked (irregular) student, who takes
    /// subjects across years instead of following one block section, is stored
    /// as year 0. Every screen and report shows a year through these helpers.
    /// </summary>
    public static class YearLevels
    {
        public const int First = 1;
        public const int Last = 5;
        public const int NonBlocked = 0;

        public const string NonBlockedName = "Non-blocked";

        /// <summary>"Year 3", "Non-blocked", or null when not set.</summary>
        public static string Label(int? year) =>
            year == null ? null : year == NonBlocked ? NonBlockedName : $"Year {year}";

        /// <summary>For narrow cells: "3", "NB", or "—".</summary>
        public static string Short(int? year) =>
            year == null ? "—" : year == NonBlocked ? "NB" : year.ToString();
    }
}
