using System;
using System.Linq;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Helpers
{
    /// <summary>Small presentation rules every screen needs to agree on.</summary>
    public static class Ui
    {
        // The ticket palette from tokens.css, in the order a new type is given one.
        private static readonly string[] Tones = { "talk", "workshop", "assembly", "training", "service" };

        /// <summary>
        /// The ticket colour for an event type — the same type is always the same
        /// colour, on the dashboard, the event list and the member's history.
        /// The seeded types get the obvious pick; anything added later is hashed
        /// into the palette so it still lands on a stable colour.
        /// </summary>
        public static string TicketType(string eventType)
        {
            var name = (eventType ?? string.Empty).Trim().ToLowerInvariant();

            if (name.Contains("assembly")) return "assembly";
            if (name.Contains("workshop") || name.Contains("training")) return "workshop";
            if (name.Contains("seminar") || name.Contains("talk") || name.Contains("lecture")) return "talk";
            if (name.Contains("sport")) return "training";
            if (name.Contains("social") || name.Contains("outreach")) return "service";

            var hash = name.Aggregate(17, (h, c) => unchecked(h * 31 + c));
            return Tones[Math.Abs(hash % Tones.Length)];
        }

        /// <summary>Status chip class for a vw_EventAttendanceSummary status.</summary>
        public static string StatusChip(string status) => status switch
        {
            Const.StatusOngoing => "chip info",
            Const.StatusUpcoming => "chip idle",
            _ => "chip ok",
        };

        /// <summary>Up to two initials for an avatar.</summary>
        public static string Initials(string name) =>
            string.Concat((name ?? string.Empty)
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(w => char.ToUpperInvariant(w[0])));

        /// <summary>"5:00 – 8:00 PM", or with both meridiems when they differ.</summary>
        public static string TimeRange(DateTime start, DateTime end)
        {
            if (start.Date != end.Date)
            {
                return $"{start:MMM d, h:mm tt} – {end:MMM d, h:mm tt}";
            }

            return start.ToString("tt") == end.ToString("tt")
                ? $"{start:h:mm} – {end:h:mm tt}"
                : $"{start:h:mm tt} – {end:h:mm tt}";
        }
    }
}
