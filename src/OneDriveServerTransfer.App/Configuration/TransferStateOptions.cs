using System.ComponentModel.DataAnnotations;

namespace OneDriveServerTransfer.Configuration;

/// <summary>
/// Application settings for the operational state schema. Only version 1 of the state
/// schema and the path-mapping rules is supported by this build; a configuration that
/// requests a newer version fails validation instead of being silently accepted.
/// </summary>
public sealed class TransferStateOptions
{
    public const string SectionName = "TransferState";

    [Range(1, 1, ErrorMessage = "Only StateSchemaVersion 1 is supported by this build.")]
    public int SchemaVersion { get; set; } = 1;

    [Range(1, 1, ErrorMessage = "Only PathMappingVersion 1 is supported by this build.")]
    public int PathMappingVersion { get; set; } = 1;
}
