using System.IO;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Streaming local hash calculation. Implemented in milestone M5. The local SHA-256 is
/// stored separately from any Microsoft source hash and is never presented as source
/// cryptographic verification.
/// </summary>
public interface IHashingService
{
    Task<string> ComputeLocalSha256HexAsync(Stream content, CancellationToken cancellationToken);
}
