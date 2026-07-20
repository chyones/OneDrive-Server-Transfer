using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.SourceResolution;

public enum EmployeeSourceInputKind
{
    Upn,
    OneDriveRootUrl
}

public sealed record ParsedOneDriveUrl(string Host, string SitePath, string AccountSegment);

public sealed record ParsedEmployeeSourceInput(
    EmployeeSourceInputKind Kind,
    string? Upn,
    ParsedOneDriveUrl? Url);

/// <summary>
/// Parses and validates the employee source input: either an employee UPN or the root
/// URL of the employee OneDrive for Business. URL mode accepts only the personal-site
/// root on a *-my.sharepoint.com host; files, subfolders, shared links, layouts pages,
/// and any query or fragment are rejected.
/// </summary>
public static partial class EmployeeSourceInputParser
{
    public static bool TryParse(
        string? input,
        out ParsedEmployeeSourceInput? parsed,
        out SourceResolutionException? error)
    {
        parsed = null;
        error = null;

        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            error = SourceResolutionErrors.InvalidEmployeeInput();
            return false;
        }

        return LooksLikeUrl(text)
            ? TryParseUrl(text, out parsed, out error)
            : TryParseUpn(text, out parsed, out error);
    }

    private static bool LooksLikeUrl(string text) =>
        text.Contains("://", StringComparison.Ordinal) ||
        text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseUpn(
        string text,
        out ParsedEmployeeSourceInput? parsed,
        out SourceResolutionException? error)
    {
        parsed = null;
        error = null;

        var atCount = text.Count(c => c == '@');
        var atIndex = text.IndexOf('@');
        var valid = text.Length is >= 3 and <= 113 &&
            atCount == 1 &&
            atIndex > 0 &&
            atIndex < text.Length - 1 &&
            !text.Contains("#EXT#", StringComparison.OrdinalIgnoreCase) &&
            UpnLocalPartRegex().IsMatch(text[..atIndex]) &&
            UpnDomainRegex().IsMatch(text[(atIndex + 1)..]);

        if (!valid)
        {
            error = SourceResolutionErrors.InvalidEmployeeInput();
            return false;
        }

        parsed = new ParsedEmployeeSourceInput(EmployeeSourceInputKind.Upn, text, null);
        return true;
    }

    private static bool TryParseUrl(
        string text,
        out ParsedEmployeeSourceInput? parsed,
        out SourceResolutionException? error)
    {
        parsed = null;
        error = null;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = SourceResolutionErrors.MalformedSourceUrl();
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !OneDriveHostRegex().IsMatch(uri.Host))
        {
            error = SourceResolutionErrors.UnsupportedSource();
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !string.Equals(segments[0], "personal", StringComparison.OrdinalIgnoreCase) ||
            !AccountSegmentRegex().IsMatch(segments[1]))
        {
            error = SourceResolutionErrors.UnsupportedSource();
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var url = new ParsedOneDriveUrl(host, $"personal/{segments[1]}", segments[1]);
        parsed = new ParsedEmployeeSourceInput(EmployeeSourceInputKind.OneDriveRootUrl, null, url);
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+$", RegexOptions.Compiled)]
    private static partial Regex UpnLocalPartRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)+$", RegexOptions.Compiled)]
    private static partial Regex UpnDomainRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*-my\.sharepoint\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OneDriveHostRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled)]
    private static partial Regex AccountSegmentRegex();
}
