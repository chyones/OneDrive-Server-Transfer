namespace OneDriveServerTransfer.Inventory;

/// <summary>
/// Facet classification of one delta item. Unknown or absent facets are never silently
/// dropped: they classify as <see cref="Unknown" /> and are treated as unsupported
/// content so the archive can never claim completeness over them.
/// </summary>
public enum DeltaItemFacet
{
    File,
    Folder,

    /// <summary>Graph package item (for example a OneNote notebook); unsupported in version 1.</summary>
    Package,

    /// <summary>Source-deletion tombstone. Recorded; never triggers local deletion.</summary>
    Deleted,

    /// <summary>External shortcut resolving to another drive; unsupported and never traversed.</summary>
    ExternalShortcut,

    /// <summary>No recognized facet; classified safely as unsupported content.</summary>
    Unknown
}

/// <summary>
/// One item of a drive delta page, reduced to the contract-required fields. Source-hash
/// information carries only supported Microsoft hashes (quickXorHash preferred, then
/// sha1Hash); the Graph sha256Hash value is never used (decision D-038).
/// </summary>
public sealed record DeltaInventoryItem(
    string ItemId,
    string? ParentItemId,
    string Name,
    long? SizeBytes,
    DeltaItemFacet Facet,
    string? ETag,
    string? CTag,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? LastModifiedUtc,
    string? SourceHashAlgorithm,
    string? SourceHashValue);

/// <summary>
/// One applied delta page. <see cref="NextLink" /> and <see cref="DeltaLink" /> are
/// opaque Microsoft URLs: they are never parsed, modified, logged, or rebuilt, and
/// exactly one of them is present on every well-formed page.
/// </summary>
public sealed record DeltaInventoryPage(
    IReadOnlyList<DeltaInventoryItem> Items,
    string? NextLink,
    string? DeltaLink)
{
    /// <summary>True on the final page of the enumeration, which carries the delta checkpoint.</summary>
    public bool IsFinal => DeltaLink is not null;
}

/// <summary>Summary of a completed delta enumeration.</summary>
public sealed record DeltaEnumerationResult(
    string DeltaCheckpoint,
    long PageCount,
    long ItemCount);
