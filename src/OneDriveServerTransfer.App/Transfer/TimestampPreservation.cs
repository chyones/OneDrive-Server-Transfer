using System.IO;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Source timestamp preservation for files and directories (contract section 8).
/// Values are applied only when Windows can represent them; values earlier than the
/// Windows file-time epoch are recorded as
/// <see cref="TimestampPreservationResult.UnsupportedValue" /> — they are never
/// fabricated or silently clamped (the documented deterministic rule). Timestamp
/// failure never invalidates verified content; it is recorded per item and forces
/// CompletedWithWarnings when no content is missing.
/// </summary>
internal static class TimestampPreservation
{
    /// <summary>The earliest instant a Windows file time can represent (1601-01-01 UTC).</summary>
    private static readonly DateTime MinWindowsFileTimeUtc =
        new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>True when the value can be represented as a Windows file time.</summary>
    public static bool IsSupportedValue(DateTimeOffset value) =>
        value.UtcDateTime >= MinWindowsFileTimeUtc;

    public static TimestampPreservationResult ApplyToFile(
        string fullPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc) =>
        Apply(fullPath, createdUtc, lastModifiedUtc,
            File.SetCreationTimeUtc, File.SetLastWriteTimeUtc);

    public static TimestampPreservationResult ApplyToDirectory(
        string fullPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc) =>
        Apply(fullPath, createdUtc, lastModifiedUtc,
            Directory.SetCreationTimeUtc, Directory.SetLastWriteTimeUtc);

    private static TimestampPreservationResult Apply(
        string fullPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc,
        Action<string, DateTime> setCreation,
        Action<string, DateTime> setLastWrite)
    {
        if (createdUtc is null && lastModifiedUtc is null)
        {
            return TimestampPreservationResult.NotAttempted;
        }

        // Deterministic rule: a source value Windows cannot represent is not applied
        // and is recorded as unsupported; representable values are still applied.
        if ((createdUtc is { } created && !IsSupportedValue(created)) ||
            (lastModifiedUtc is { } modified && !IsSupportedValue(modified)))
        {
            return TimestampPreservationResult.UnsupportedValue;
        }

        try
        {
            if (createdUtc is { } supportedCreated)
            {
                setCreation(fullPath, supportedCreated.UtcDateTime);
            }

            if (lastModifiedUtc is { } supportedModified)
            {
                setLastWrite(fullPath, supportedModified.UtcDateTime);
            }

            return TimestampPreservationResult.Preserved;
        }
        catch (ArgumentOutOfRangeException)
        {
            return TimestampPreservationResult.UnsupportedValue;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TimestampPreservationResult.Failed;
        }
    }
}
