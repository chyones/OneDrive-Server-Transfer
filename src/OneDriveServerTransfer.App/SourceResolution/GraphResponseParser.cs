using System.Text.Json;

namespace OneDriveServerTransfer.SourceResolution;

/// <summary>Parsed fields of an approved drive response (unknown properties ignored).</summary>
public sealed record GraphDriveData(
    string? Id,
    string? DriveType,
    string? WebUrl,
    string? OwnerUserId,
    string? OwnerDisplayName,
    long? QuotaTotalBytes,
    long? QuotaUsedBytes,
    long? QuotaRemainingBytes);

/// <summary>Parsed fields of an approved site response (unknown properties ignored).</summary>
public sealed record GraphSiteData(
    string? Id,
    string? WebUrl,
    bool? IsPersonalSite,
    string? SiteCollectionHostName);

/// <summary>
/// Tolerant parsers for the approved Graph v1.0 resolution responses. Unknown JSON
/// properties are ignored; missing required fields surface as nulls and are rejected by
/// the resolver as unexpected responses rather than crashing deserialization.
/// </summary>
public static class GraphResponseParser
{
    public static GraphDriveData ParseDrive(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        string? ownerId = null;
        string? ownerName = null;

        if (root.TryGetProperty("owner", out var owner) &&
            owner.TryGetProperty("user", out var user))
        {
            ownerId = GetString(user, "id");
            ownerName = GetString(user, "displayName");
        }

        long? quotaTotal = null;
        long? quotaUsed = null;
        long? quotaRemaining = null;
        if (root.TryGetProperty("quota", out var quota))
        {
            quotaTotal = GetInt64(quota, "total");
            quotaUsed = GetInt64(quota, "used");
            quotaRemaining = GetInt64(quota, "remaining");
        }

        return new GraphDriveData(
            GetString(root, "id"),
            GetString(root, "driveType"),
            GetString(root, "webUrl"),
            ownerId,
            ownerName,
            quotaTotal,
            quotaUsed,
            quotaRemaining);
    }

    public static GraphSiteData ParseSite(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        string? hostName = null;
        if (root.TryGetProperty("siteCollection", out var siteCollection))
        {
            hostName = GetString(siteCollection, "hostname");
        }

        bool? isPersonalSite = root.TryGetProperty("isPersonalSite", out var flag) &&
            flag.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? flag.GetBoolean()
                : null;

        return new GraphSiteData(
            GetString(root, "id"),
            GetString(root, "webUrl"),
            isPersonalSite,
            hostName);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : null;
}
