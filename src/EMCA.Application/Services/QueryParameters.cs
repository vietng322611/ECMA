using System.Globalization;
using System.Text;

namespace EMCA.Application.Services;

// Raw query values: null when absent; controllers pass MultipleValues when a key is repeated.
public static class QueryParameters
{
    public const string MultipleValues = "\0multiple";
    public const int MaxKeywordLength = 100;

    // SRS §2.2: absent → 1; otherwise ^[0-9]+$ within 1..int.MaxValue.
    public static bool TryPage(string? raw, out int page)
    {
        page = 1;
        if (raw is null) return true;
        return raw.Length > 0 && raw.All(char.IsAsciiDigit) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1;
    }

    public static bool TryKeyword(string? raw, out string? keyword)
    {
        keyword = null;
        if (raw == MultipleValues) return false;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        keyword = raw.Normalize(NormalizationForm.FormC).Trim();
        return keyword.EnumerateRunes().Count() <= MaxKeywordLength;
    }

    // Filters default to "All" when absent; empty, unknown or repeated values are invalid (BR-EVT-08 style).
    public static bool TryChoice<T>(string? raw, IReadOnlyDictionary<string, T> choices, out T? value) where T : struct
    {
        value = null;
        if (raw is null or "All") return true;
        if (!choices.TryGetValue(raw, out var parsed)) return false;
        value = parsed;
        return true;
    }

    public static bool TryDate(string? raw, out DateOnly? date)
    {
        date = null;
        if (raw == MultipleValues) return false;
        if (string.IsNullOrEmpty(raw)) return true;
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        date = parsed;
        return true;
    }
}
