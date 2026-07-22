using System.Globalization;
using System.Text.Json;

namespace OneDriveServerTransfer.Inventory;

/// <summary>
/// Tolerant parser for Microsoft Graph v1.0 drive delta pages (GRAPH-SCAN-001). Unknown
/// JSON properties are ignored, conditionally absent fields surface as nulls, and items
/// without any recognized facet classify as <see cref="DeltaItemFacet.Unknown" /> so
/// they are never silently dropped. Paging links are extracted as opaque strings.
/// </summary>
public static class GraphDeltaResponseParser
{
    /// <summary>
    /// Parses one delta page. Throws <see cref="InventoryException" /> when the page
    /// structure is a protocol failure: missing value array, an item without an ID, or
    /// a page that carries neither or both paging links.
    /// </summary>
    public static DeltaInventoryPage ParseDeltaPage(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw InventoryErrors.MalformedDeltaResponse();
        }

        var nextLink = GetString(root, "@odata.nextLink");
        var deltaLink = GetString(root, "@odata.deltaLink");

        // Exactly one paging link must be present; anything else is a protocol failure
        // that must fail safely rather than risk skipping or re-applying data.
        if ((nextLink is null) == (deltaLink is null))
        {
            throw InventoryErrors.MalformedDeltaResponse();
        }

        var items = new List<DeltaInventoryItem>(value.GetArrayLength());
        foreach (var element in value.EnumerateArray())
        {
            items.Add(ParseItem(element));
        }

        return new DeltaInventoryPage(items, nextLink, deltaLink);
    }

    /// <summary>
    /// Parses one drive item element (delta page item or GRAPH-ITEM-001 single-item
    /// response). Shared by the delta inventory and the item metadata re-read.
    /// </summary>
    public static DeltaInventoryItem ParseItem(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InventoryErrors.MalformedDeltaResponse();
        }

        var itemId = GetString(element, "id");
        if (string.IsNullOrEmpty(itemId))
        {
            // An item without a durable Drive Item ID cannot be tracked safely.
            throw InventoryErrors.MalformedDeltaResponse();
        }

        string? parentItemId = null;
        if (element.TryGetProperty("parentReference", out var parentReference) &&
            parentReference.ValueKind == JsonValueKind.Object)
        {
            // parentReference.path is deliberately never read: the hierarchy is rebuilt
            // from item IDs because the path property is conditionally absent.
            parentItemId = GetString(parentReference, "id");
        }

        var facet = ClassifyFacet(element);
        var (hashAlgorithm, hashValue) = facet == DeltaItemFacet.File
            ? ReadSourceHash(element)
            : (null, null);

        return new DeltaInventoryItem(
            itemId,
            parentItemId,
            GetString(element, "name") ?? string.Empty,
            GetInt64(element, "size"),
            facet,
            GetString(element, "eTag"),
            GetString(element, "cTag"),
            GetDateTimeOffset(element, "createdDateTime"),
            GetDateTimeOffset(element, "lastModifiedDateTime"),
            hashAlgorithm,
            hashValue);
    }

    /// <summary>
    /// Facet precedence: a deletion tombstone wins over every other facet; packages and
    /// external shortcuts are unsupported content; an item with no recognized facet is
    /// classified unknown rather than dropped.
    /// </summary>
    private static DeltaItemFacet ClassifyFacet(JsonElement element)
    {
        if (HasObjectFacet(element, "deleted"))
        {
            return DeltaItemFacet.Deleted;
        }

        if (HasObjectFacet(element, "package"))
        {
            return DeltaItemFacet.Package;
        }

        if (HasObjectFacet(element, "remoteItem"))
        {
            return DeltaItemFacet.ExternalShortcut;
        }

        if (HasObjectFacet(element, "file"))
        {
            return DeltaItemFacet.File;
        }

        if (HasObjectFacet(element, "folder"))
        {
            return DeltaItemFacet.Folder;
        }

        return DeltaItemFacet.Unknown;
    }

    /// <summary>
    /// Reads the supported Microsoft source hash: quickXorHash first (preferred per
    /// D-038), then sha1Hash. The Graph sha256Hash value is never used (D-038); unknown
    /// hash properties are ignored. Absence of a comparable hash is valid and stays
    /// null.
    /// </summary>
    private static (string? Algorithm, string? Value) ReadSourceHash(JsonElement element)
    {
        if (!element.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.Object ||
            !file.TryGetProperty("hashes", out var hashes) || hashes.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        var quickXor = GetString(hashes, "quickXorHash");
        if (!string.IsNullOrEmpty(quickXor))
        {
            return ("quickXorHash", quickXor);
        }

        var sha1 = GetString(hashes, "sha1Hash");
        return !string.IsNullOrEmpty(sha1) ? ("sha1Hash", sha1) : (null, null);
    }

    private static bool HasObjectFacet(JsonElement element, string facet) =>
        element.TryGetProperty(facet, out var value) && value.ValueKind == JsonValueKind.Object;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string property)
    {
        var text = GetString(element, property);
        return text is not null &&
               DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}
