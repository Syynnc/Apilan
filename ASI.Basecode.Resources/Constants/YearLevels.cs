namespace ASI.Basecode.Resources.Constants
{
    /* Year levels run 1 to 5. A non-blocked (irregular) student, who takes
       subjects across years instead of following one block section, is stored
       as year 0. Every screen and report shows a year through these helpers. */
    public static class YearLevels
    {
        public const int First = 1;
        public const int Last = 5;
        public const int NonBlocked = 0;

        public const string NonBlockedName = "Non-blocked";

        // "Year 3", "Non-blocked", or null when not set.
        public static string Label(int? year) =>
            year == null ? null : year == NonBlocked ? NonBlockedName : $"Year {year}";

        // For narrow cells: "3", "NB", or "—".
        public static string Short(int? year) =>
            year == null ? "—" : year == NonBlocked ? "NB" : year.ToString();
    }
}
