using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ASI.Basecode.WebApp.Helpers
{
    /// <summary>
    /// Says what an edit changed, for the activity log: "Venue: AVR → Lab 1;
    /// Capacity: 40 → 50". Compares the simple fields of two copies of a form
    /// model, by their [Display] names.
    /// </summary>
    public static class AuditDiff
    {
        public static string Changes<T>(T before, T after, IDictionary<string, Func<object, string>> show = null)
        {
            if (before == null || after == null)
            {
                return null;
            }

            var changes = new List<string>();
            foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name == "Id" || !p.CanRead || !IsSimple(p.PropertyType))
                {
                    continue;
                }

                var was = p.GetValue(before);
                var now = p.GetValue(after);
                if (Same(was, now))
                {
                    continue;
                }

                var format = show != null && show.TryGetValue(p.Name, out var f) ? f : Text;
                changes.Add($"{Label(p)}: {format(was)} → {format(now)}");
            }

            return changes.Count == 0 ? "Saved with no changes" : string.Join("; ", changes);
        }

        private static bool IsSimple(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
                   || !(typeof(IEnumerable).IsAssignableFrom(t) || t.IsClass);
        }

        private static bool Same(object a, object b)
        {
            // A blank box and an empty value are the same thing to the person editing.
            if (a is string sa && string.IsNullOrWhiteSpace(sa)) { a = null; }
            if (b is string sb && string.IsNullOrWhiteSpace(sb)) { b = null; }
            if (a is string ta && b is string tb) { return ta.Trim() == tb.Trim(); }
            return Equals(a, b);
        }

        private static string Text(object value) =>
            value switch
            {
                null => "—",
                string s when string.IsNullOrWhiteSpace(s) => "—",
                string s => s.Length > 60 ? s.Substring(0, 59) + "…" : s,
                DateTime d => d.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture),
                bool b => b ? "yes" : "no",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };

        private static string Label(PropertyInfo p) =>
            p.GetCustomAttribute<DisplayAttribute>()?.GetName()
            ?? Regex.Replace(p.Name, "(?<=[a-z])([A-Z])", " $1");
    }
}
