using System;
using System.Linq;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Helpers
{
    /// <summary>Small presentation rules every screen needs to agree on.</summary>
    public static class Ui
    {
        // The ticket palette from tokens.css, in the order a new type is given one.
        public static readonly string[] Tones = { "talk", "workshop", "assembly", "training", "service" };

        // Colours officers picked for a type, by lower-cased name.
        private static volatile System.Collections.Generic.Dictionary<string, string> _chosen = new();

        public static void SetChosenTones(System.Collections.Generic.IEnumerable<(string Name, string Color)> types)
        {
            _chosen = types
                .Where(t => !string.IsNullOrWhiteSpace(t.Name) && Tones.Contains(t.Color))
                .GroupBy(t => t.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Color);
        }

        /// <summary>
        /// The ticket colour for an event type — the same type is always the same
        /// colour, on the dashboard, the event list and the member's history.
        /// The seeded types get the obvious pick; anything added later is hashed
        /// into the palette so it still lands on a stable colour.
        /// </summary>
        public static string TicketType(string eventType)
        {
            var name = (eventType ?? string.Empty).Trim().ToLowerInvariant();
            if (name.Length == 0) return "talk";
            if (_chosen.TryGetValue(name, out var chosen)) return chosen;

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

        /// <summary>"in 3 days", "in 2h 15m", "tomorrow" — how far off a moment is, for a glance.</summary>
        public static string Until(DateTime target, DateTime? now = null)
        {
            var from = now ?? DateTime.Now;
            var span = target - from;
            if (span.TotalMinutes < 1) return "now";
            if (span.TotalMinutes < 60) return $"in {(int)span.TotalMinutes}m";
            if (span.TotalHours < 24 && target.Date == from.Date) return $"in {(int)span.TotalHours}h {span.Minutes}m";
            if (target.Date == from.Date.AddDays(1)) return "tomorrow";
            var days = (target.Date - from.Date).Days;
            return days < 14 ? $"in {days} days" : $"in {days / 7} weeks";
        }

        /// <summary>"2 days ago", "3h ago" — how long since a moment.</summary>
        public static string Since(DateTime past, DateTime? now = null)
        {
            var span = (now ?? DateTime.Now) - past;
            if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 2) return "yesterday";
            return span.TotalDays < 14 ? $"{(int)span.TotalDays} days ago" : $"{(int)(span.TotalDays / 7)} weeks ago";
        }

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
