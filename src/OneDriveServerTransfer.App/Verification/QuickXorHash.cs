namespace OneDriveServerTransfer.Verification;

/// <summary>
/// Incremental implementation of the Microsoft QuickXorHash algorithm used for the
/// OneDrive <c>quickXorHash</c> file hash. This is a non-cryptographic 160-bit hash:
/// every input byte is XORed into a circular 160-bit shift register that advances 11
/// bits per byte, and the 64-bit little-endian content length is XORed into the final
/// 64 bits. The implementation follows the official Microsoft reference sample and is
/// streaming: memory use is constant regardless of content size. It is used only to
/// verify the Microsoft source hash and is never presented as a local cryptographic
/// hash (D-038).
/// </summary>
internal sealed class QuickXorHash
{
    private const int WidthInBits = 160;
    private const int Shift = 11;
    private const int HashLengthBytes = WidthInBits / 8;

    private readonly byte[] _hash = new byte[HashLengthBytes];
    private int _shiftSoFarBits;
    private long _lengthSoFar;

    /// <summary>Adds one chunk of content to the running hash.</summary>
    public void Update(ReadOnlySpan<byte> buffer)
    {
        foreach (var value in buffer)
        {
            var byteIndex = _shiftSoFarBits / 8;
            var bitOffset = _shiftSoFarBits % 8;
            var shifted = value << bitOffset;

            _hash[byteIndex] ^= (byte)(shifted & 0xFF);
            _hash[(byteIndex + 1) % HashLengthBytes] ^= (byte)(shifted >> 8);

            _shiftSoFarBits = (_shiftSoFarBits + Shift) % WidthInBits;
            _lengthSoFar++;
        }
    }

    /// <summary>
    /// Returns the 20-byte hash: the shift register with the 64-bit little-endian
    /// content length XORed into its final 64 bits.
    /// </summary>
    public byte[] FinalizeHash()
    {
        var result = (byte[])_hash.Clone();
        var length = _lengthSoFar;
        for (var index = 0; index < 8; index++)
        {
            result[HashLengthBytes - 8 + index] ^= (byte)(length & 0xFF);
            length >>= 8;
        }

        return result;
    }
}
