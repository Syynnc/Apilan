using System.Security.Cryptography;

namespace Apilan.Services;

/// <summary>
/// Generates the short check-in code printed on a registration stub, e.g.
/// APL-7K2M. Codes are typed by hand at the door, so the alphabet excludes
/// characters people confuse: I/1, O/0, S/5, Z/2, B/8, U/V.
/// </summary>
public static class RegistrationCode
{
    private const string Alphabet = "ACDEFGHJKLMNPQRTWXY34679";
    private const string Prefix = "APL-";
    private const int Length = 4;

    public static string Next()
    {
        Span<char> body = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            body[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return Prefix + new string(body);
    }

    /// <summary>
    /// Normalises whatever the door typed or scanned into canonical form, so
    /// "apl7k2m", "APL 7K2M" and "apl-7k2m" all resolve to the same code.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var cleaned = new string(raw
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (cleaned.StartsWith("APL", StringComparison.Ordinal))
        {
            cleaned = cleaned[3..];
        }

        return cleaned.Length == 0 ? string.Empty : Prefix + cleaned;
    }
}
