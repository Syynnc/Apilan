using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// The readable name an event goes by in its URL — its name and date,
    /// e.g. "general-assembly-2026-11-16" — so links never show the row id.
    /// </summary>
    public static class EventSlug
    {
        public static string For(string name, DateTime start)
        {
            var text = (name ?? string.Empty).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            var dash = false;
            foreach (var c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                    dash = false;
                }
                else if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }

            var stem = sb.ToString().Trim('-');
            if (stem.Length > 60) stem = stem.Substring(0, 60).TrimEnd('-');
            return (stem.Length > 0 ? stem + "-" : "event-") + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
