using System.IO;
using System.Security.Cryptography;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Verification;

/// <summary>
/// Streaming hash service (D-038). Every method reads the stream in bounded chunks,
/// observes cancellation during the read, and never buffers the full content. The
/// local SHA-256 (lowercase hex) is computed and stored separately from the Microsoft
/// source hashes (base64), and the Graph sha256Hash value is never accepted here:
/// only <c>quickXorHash</c> and <c>sha1Hash</c> are supported source-hash algorithms.
/// </summary>
public sealed class HashingService : IHashingService
{
    /// <summary>Supported Microsoft source-hash algorithm names (D-038).</summary>
    public const string QuickXorHashAlgorithm = "quickXorHash";
    public const string Sha1Algorithm = "sha1Hash";

    private const int BufferSizeBytes = 80 * 1024;

    public async Task<string> ComputeLocalSha256HexAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var sha256 = SHA256.Create();
        var hash = await ComputeAsync(
            content,
            (buffer, length) => sha256.TransformBlock(buffer, 0, length, null, 0),
            sha256,
            cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<string> ComputeQuickXorHashBase64Async(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var quickXor = new QuickXorHash();
        await ConsumeAsync(
            content,
            (buffer, length) => quickXor.Update(buffer.AsSpan(0, length)),
            cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(quickXor.FinalizeHash());
    }

    public async Task<string> ComputeSha1Base64Async(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var sha1 = SHA1.Create();
        var hash = await ComputeAsync(
            content,
            (buffer, length) => sha1.TransformBlock(buffer, 0, length, null, 0),
            sha1,
            cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(hash);
    }

    public async Task<bool> VerifySourceHashAsync(
        Stream content,
        string sourceHashAlgorithm,
        string expectedBase64Value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBase64Value);

        var actual = sourceHashAlgorithm switch
        {
            QuickXorHashAlgorithm => await ComputeQuickXorHashBase64Async(content, cancellationToken)
                .ConfigureAwait(false),
            Sha1Algorithm => await ComputeSha1Base64Async(content, cancellationToken)
                .ConfigureAwait(false),
            // The Graph sha256Hash value and every other algorithm are unsupported
            // (D-038); an unknown algorithm can never be silently treated as verified.
            _ => throw new NotSupportedException(
                $"Source hash algorithm '{sourceHashAlgorithm}' is not supported."),
        };

        return string.Equals(actual, expectedBase64Value, StringComparison.Ordinal);
    }

    private static async Task<byte[]> ComputeAsync(
        Stream content,
        Action<byte[], int> update,
        HashAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        await ConsumeAsync(content, update, cancellationToken).ConfigureAwait(false);
        algorithm.TransformFinalBlock([], 0, 0);
        return algorithm.Hash!;
    }

    private static async Task ConsumeAsync(
        Stream content,
        Action<byte[], int> update,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSizeBytes];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            update(buffer, read);
        }
    }
}
