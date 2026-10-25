namespace ECMA.Web.Services;

public static class ReturnUrlPolicy
{
    private static readonly HashSet<string> AllowedGetPaths = new(StringComparer.Ordinal)
    {
        "/", "/privacy", "/account/profile", "/account/change-password", "/my-registrations"
    };

    public static string? Canonicalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\')) return null;

        string decoded = value;
        for (var i = 0; i < 4; i++)
        {
            string next;
            try { next = Uri.UnescapeDataString(decoded); }
            catch (UriFormatException) { return null; }
            if (next == decoded) break;
            decoded = next;
        }

        if (!decoded.StartsWith('/') || decoded.StartsWith("//") || decoded.Contains('\\') || decoded.Any(char.IsControl) ||
            !Uri.TryCreate("https://local" + decoded, UriKind.Absolute, out var uri) || uri.Host != "local" ||
            !AllowedGetPaths.Contains(uri.AbsolutePath) && !IsPublicEventDetails(uri.AbsolutePath)) return null;

        return uri.PathAndQuery + uri.Fragment;
    }

    // Guest "register" on event details: /events/{id} with a lowercase GUID (FR-SRCH-02, SRS §4.1).
    private static bool IsPublicEventDetails(string path) =>
        path.StartsWith("/events/", StringComparison.Ordinal) && Guid.TryParseExact(path["/events/".Length..], "D", out var id) &&
        path == "/events/" + id.ToString("D");
}
