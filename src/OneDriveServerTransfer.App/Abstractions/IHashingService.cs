using System.IO;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Streaming local hash calculation and Microsoft source-hash verification (D-038).
/// Implemented in milestone M5. The local SHA-256 is stored separately from any
/// Microsoft source hash and is never presented as source cryptographic verification.
/// Supported Microsoft source hashes are quickXorHash (preferred) and sha1Hash; the
/// Graph sha256Hash value is never used. All methods stream with bounded memory and
/// observe cancellation.
/// </summary>
public interface IHashingService
{
    /// <summary>Streaming SHA-256 over the local content, lowercase hexadecimal.</summary>
    Task<string> ComputeLocalSha256HexAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>Streaming Microsoft QuickXorHash over the content, base64.</summary>
    Task<string> ComputeQuickXorHashBase64Async(Stream content, CancellationToken cancellationToken);

    /// <summary>Streaming SHA-1 over the content, base64 (the Graph sha1Hash encoding).</summary>
    Task<string> ComputeSha1Base64Async(Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies content against a supported Microsoft source hash
    /// (<c>quickXorHash</c> or <c>sha1Hash</c>). Returns false for a mismatch and throws
    /// <see cref="NotSupportedException" /> for any other algorithm name so an
    /// unsupported value can never be silently treated as verified.
    /// </summary>
    Task<bool> VerifySourceHashAsync(
        Stream content,
        string sourceHashAlgorithm,
        string expectedBase64Value,
        CancellationToken cancellationToken);
}
