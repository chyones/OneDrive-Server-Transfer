using OneDriveServerTransfer.Verification;

namespace OneDriveServerTransfer.Tests.Verification;

/// <summary>
/// Verifies the streaming hash service: known vectors, streaming behavior, Microsoft
/// source-hash verification rules (D-038), and cancellation.
/// </summary>
public class HashingServiceTests
{
    private readonly HashingService _service = new();

    [Fact]
    public async Task LocalSha256MatchesKnownVector()
    {
        var result = await _service.ComputeLocalSha256HexAsync(
            new MemoryStream("abc"u8.ToArray()), CancellationToken.None);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result);
    }

    [Fact]
    public async Task QuickXorHashOfEmptyContentMatchesKnownZeroVector()
    {
        var result = await _service.ComputeQuickXorHashBase64Async(
            new MemoryStream([]), CancellationToken.None);

        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAA=", result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(160)]
    [InlineData(161)]
    [InlineData(100_000)]
    public async Task QuickXorHashIsStableAcrossChunkings(int length)
    {
        var content = new byte[length];
        new Random(length).NextBytes(content);

        var oneShot = await _service.ComputeQuickXorHashBase64Async(
            new MemoryStream(content), CancellationToken.None);
        var chunked = await _service.ComputeQuickXorHashBase64Async(
            new ChunkedReadStream(content, 997), CancellationToken.None);

        Assert.Equal(oneShot, chunked);
    }

    [Fact]
    public async Task Sha1MatchesKnownVector()
    {
        var result = await _service.ComputeSha1Base64Async(
            new MemoryStream("abc"u8.ToArray()), CancellationToken.None);

        Assert.Equal("qZk+NkcGgWq6PiVxeFDCbJzQ2J0=", result);
    }

    [Fact]
    public async Task VerifySourceHashSucceedsOnMatchAndFailsOnMismatch()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("employee content");
        var expected = await _service.ComputeQuickXorHashBase64Async(
            new MemoryStream(content), CancellationToken.None);

        Assert.True(await _service.VerifySourceHashAsync(
            new MemoryStream(content), "quickXorHash", expected, CancellationToken.None));
        Assert.False(await _service.VerifySourceHashAsync(
            new MemoryStream(content), "quickXorHash", "AAAAAAAAAAAAAAAAAAAAAAAAAAA=", CancellationToken.None));
    }

    [Fact]
    public async Task GraphSha256HashIsNeverAccepted()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => _service.VerifySourceHashAsync(
            new MemoryStream([1, 2, 3]), "sha256Hash", "anything", CancellationToken.None));
    }

    [Fact]
    public async Task CancellationDuringHashingObserved()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ComputeLocalSha256HexAsync(new MemoryStream(new byte[1024]), cts.Token));
    }

    /// <summary>Stream that serves content in fixed-size chunks to exercise chunked reads.</summary>
    private sealed class ChunkedReadStream(byte[] content, int chunkSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = content.Length - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var length = Math.Min(Math.Min(count, chunkSize), remaining);
            Array.Copy(content, _position, buffer, offset, length);
            _position += length;
            return length;
        }
    }
}
