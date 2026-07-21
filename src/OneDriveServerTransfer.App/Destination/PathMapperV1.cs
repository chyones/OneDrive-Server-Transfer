using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OneDriveServerTransfer.Destination;

/// <summary>Input for mapping one source item name under an already-mapped parent.</summary>
public sealed record PathMapRequest(
    string ParentKey,
    string SourceName,
    string SourceItemId,
    MappedItemKind Kind);

/// <summary>The deterministic mapping of one source item name.</summary>
public sealed record PathMapResult(
    string MappedName,
    string RelativePath,
    bool UsedCollisionSuffix);

/// <summary>
/// Deterministic Windows-safe source-to-local path mapping. Version 1 implements
/// exactly the ten binding rules of contract section 11; changing any rule requires a
/// new <c>PathMappingVersion</c>.
/// </summary>
public interface IPathMapper
{
    int Version { get; }

    PathMapResult Map(PathMapRequest request);
}

/// <summary>
/// PathMappingVersion = 1. Pure and deterministic: no filesystem, Graph, or clock
/// dependencies. Collision state lives only in the injected
/// <see cref="IPathCollisionRegistry" />.
/// </summary>
public sealed class PathMapperV1 : IPathMapper
{
    /// <summary>Maximum UTF-16 code units per mapped component (rule 8).</summary>
    public const int MaxComponentUtf16Units = 200;

    /// <summary>
    /// Maximum UTF-16 code units for the canonical final path (rule 9): the Windows
    /// long-path limit of 32767 characters. Paths beyond it fail the item with the
    /// stable <c>DST-PATH-006</c> error; they are never shortened non-deterministically.
    /// </summary>
    public const int MaxCanonicalPathUtf16Units = 32767;

    private static readonly int[] SuffixHexLengths = [10, 20, 64];

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly IPathCollisionRegistry _registry;

    public PathMapperV1(IPathCollisionRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public int Version => 1;

    public PathMapResult Map(PathMapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ParentKey);
        ArgumentNullException.ThrowIfNull(request.SourceName);
        ArgumentException.ThrowIfNullOrEmpty(request.SourceItemId);

        // Rules 1-3: NFC normalize, encode invalid characters and trailing dots/spaces,
        // and prefix reserved device names (files and folders alike).
        var encoded = ApplyReservedDevicePrefix(
            EncodeComponent(request.SourceName.Normalize(NormalizationForm.FormC)));

        // Rule 4: an empty mapped component becomes _empty_ plus the collision suffix.
        var forceSuffix = encoded.Length == 0;
        var stem = forceSuffix ? "_empty_" : encoded;

        string extension;
        if (!forceSuffix && request.Kind == MappedItemKind.File)
        {
            SplitExtension(stem, out stem, out extension);
        }
        else
        {
            extension = string.Empty;
        }

        var itemHashHex = ComputeItemHashHex(request.SourceItemId);

        if (!forceSuffix)
        {
            var natural = Assemble(stem, suffix: null, extension, request.Kind);
            var existing = _registry.Find(request.ParentKey, natural);
            if (existing is null)
            {
                _registry.Register(request.ParentKey, natural,
                    new PathCollisionEntry(request.SourceItemId, request.Kind));
                return BuildResult(request.ParentKey, natural, usedCollisionSuffix: false);
            }

            if (IsSameItem(existing, request))
            {
                return BuildResult(request.ParentKey, natural, usedCollisionSuffix: false);
            }
        }

        // Rules 6-7: deterministic collision suffix, expanding 10 -> 20 -> full SHA-256.
        foreach (var hexLength in SuffixHexLengths)
        {
            var suffix = "~" + itemHashHex[..hexLength];
            var candidate = Assemble(stem, suffix, extension, request.Kind);
            var existing = _registry.Find(request.ParentKey, candidate);
            if (existing is null)
            {
                _registry.Register(request.ParentKey, candidate,
                    new PathCollisionEntry(request.SourceItemId, request.Kind));
                return BuildResult(request.ParentKey, candidate, usedCollisionSuffix: true);
            }

            if (IsSameItem(existing, request))
            {
                return BuildResult(request.ParentKey, candidate, usedCollisionSuffix: true);
            }
        }

        // Two distinct drive item IDs produced the same full SHA-256: not representable.
        throw new InvalidOperationException(
            "PathMappingVersion 1 could not produce a unique deterministic name for a source item.");
    }

    private static bool IsSameItem(PathCollisionEntry entry, PathMapRequest request) =>
        entry.Kind == request.Kind &&
        string.Equals(entry.SourceItemId, request.SourceItemId, StringComparison.Ordinal);

    private static PathMapResult BuildResult(string parentKey, string mappedName, bool usedCollisionSuffix)
    {
        var relativePath = parentKey.Length == 0 ? mappedName : parentKey + "\\" + mappedName;
        return new PathMapResult(mappedName, relativePath, usedCollisionSuffix);
    }

    /// <summary>
    /// Rule 2: encode Windows-invalid characters, ASCII control characters, and
    /// trailing dots and spaces as <c>_xHHHH_</c> (uppercase four-digit UTF-16 code
    /// unit value). Valid supplementary characters pass through unchanged.
    /// </summary>
    private static string EncodeComponent(string component)
    {
        if (component.Length == 0)
        {
            return component;
        }

        var trailingCount = 0;
        while (trailingCount < component.Length &&
               (component[component.Length - 1 - trailingCount] is '.' or ' '))
        {
            trailingCount++;
        }

        var builder = new StringBuilder(component.Length + (trailingCount * 6));
        for (var index = 0; index < component.Length; index++)
        {
            var c = component[index];
            var mustEncode = index >= component.Length - trailingCount ||
                             IsWindowsInvalidCharacter(c) ||
                             IsAsciiControlCharacter(c);

            if (mustEncode)
            {
                builder.Append("_x");
                builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsWindowsInvalidCharacter(char c) =>
        c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';

    private static bool IsAsciiControlCharacter(char c) => c <= '\u001F';

    /// <summary>
    /// Rule 3: a component whose pre-extension stem is a Windows reserved device name
    /// is prefixed with <c>_</c> after normalization and encoding.
    /// </summary>
    private static string ApplyReservedDevicePrefix(string encodedName)
    {
        var stemEnd = encodedName.IndexOf('.', StringComparison.Ordinal);
        var deviceStem = stemEnd < 0 ? encodedName : encodedName[..stemEnd];
        return ReservedDeviceNames.Contains(deviceStem) ? "_" + encodedName : encodedName;
    }

    /// <summary>
    /// Extension split for files: the extension is the final dot segment when a dot
    /// exists after the first character and before the last.
    /// </summary>
    private static void SplitExtension(string encodedName, out string stem, out string extension)
    {
        var lastDot = encodedName.LastIndexOf('.');
        if (lastDot > 0 && lastDot < encodedName.Length - 1)
        {
            stem = encodedName[..lastDot];
            extension = encodedName[lastDot..];
        }
        else
        {
            stem = encodedName;
            extension = string.Empty;
        }
    }

    /// <summary>
    /// Rule 8: cap each mapped component at 200 UTF-16 code units, truncating only the
    /// human-readable stem while retaining the deterministic suffix and as much of the
    /// extension as fits. Truncation never splits a surrogate pair and never leaves a
    /// trailing dot or space.
    /// </summary>
    private static string Assemble(string stem, string? suffix, string extension, MappedItemKind kind)
    {
        var suffixLength = suffix?.Length ?? 0;
        var extensionPart = string.Empty;

        var stemBudget = MaxComponentUtf16Units - suffixLength;
        if (kind == MappedItemKind.File)
        {
            var extensionKeep = Math.Min(extension.Length, stemBudget);
            extensionPart = extension[..extensionKeep];
            stemBudget -= extensionKeep;
        }

        var stemPart = TruncateUtf16(stem, Math.Max(stemBudget, 0)).TrimEnd('.', ' ');
        return stemPart + suffix + extensionPart;
    }

    private static string TruncateUtf16(string value, int maxUnits)
    {
        if (value.Length <= maxUnits)
        {
            return value;
        }

        var cut = maxUnits;
        if (cut > 0 && char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return value[..cut];
    }

    /// <summary>Rule 6: lowercase SHA-256 hex over the UTF-8 source Drive Item ID.</summary>
    private static string ComputeItemHashHex(string sourceItemId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceItemId)));
}
