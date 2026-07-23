using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Transfer;

namespace OneDriveServerTransfer.Tests.Transfer;

/// <summary>
/// Verifies the single retry owner for temporary download requests: Retry-After,
/// bounded backoff with jitter, the five-attempt per-file budget, responsive
/// cancellation, and category exclusivity (Graph requests are never retried here).
/// </summary>
public class DownloadRetryCoordinatorTests
{
    private static DownloadRetryCoordinator Create(IList<TimeSpan>? delays = null, Func<double>? jitter = null) =>
        new(
            NullLogger<DownloadRetryCoordinator>.Instance,
            (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            },
            jitter ?? (() => 0.0));

    private static TemporaryDownloadException Transient(int? status = 503, TimeSpan? retryAfter = null) =>
        new(TemporaryDownloadFailureKind.Transient, status, retryAfter);

    [Fact]
    public async Task SuccessOnFirstAttemptDoesNotRetry()
    {
        var coordinator = Create();
        var attempts = 0;

        var result = await coordinator.ExecuteAsync(
            RetryCategory.TemporaryDownload,
            _ =>
            {
                attempts++;
                return Task.FromResult(42);
            },
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task TransientFailureIsRetriedWithBackoff()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays);
        var attempts = 0;

        var result = await coordinator.ExecuteAsync(
            RetryCategory.TemporaryDownload,
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(Transient())
                    : Task.FromResult(7);
            },
            CancellationToken.None);

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(delays));
    }

    [Fact]
    public async Task RetryAfterIsHonoredOverBackoff()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays);
        var attempts = 0;

        await coordinator.ExecuteAsync<int>(
            RetryCategory.TemporaryDownload,
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(Transient(429, TimeSpan.FromSeconds(5)))
                    : Task.FromResult(1);
            },
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(delays));
    }

    [Fact]
    public async Task BudgetIsBoundedAtFiveAttemptsPerFile()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() =>
            coordinator.ExecuteAsync<int>(
                RetryCategory.TemporaryDownload,
                _ =>
                {
                    attempts++;
                    return Task.FromException<int>(Transient());
                },
                CancellationToken.None));

        Assert.Equal(DownloadRetryCoordinator.MaxAttempts, attempts);
        Assert.Equal(DownloadRetryCoordinator.MaxAttempts - 1, delays.Count);
        Assert.Equal(503, exception.StatusCode);
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        var coordinator = Create();
        var attempts = 0;

        await Assert.ThrowsAsync<TemporaryDownloadException>(() => coordinator.ExecuteAsync<int>(
            RetryCategory.TemporaryDownload,
            _ =>
            {
                attempts++;
                return Task.FromException<int>(
                    new TemporaryDownloadException(TemporaryDownloadFailureKind.Permanent, 400));
            },
            CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task CancellationDuringDelayStopsPromptly()
    {
        using var cts = new CancellationTokenSource();
        var coordinator = new DownloadRetryCoordinator(
            NullLogger<DownloadRetryCoordinator>.Instance,
            async (delay, ct) =>
            {
                cts.Cancel();
                await Task.Delay(delay, ct);
            },
            () => 0.0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ExecuteAsync<int>(
            RetryCategory.TemporaryDownload,
            _ => Task.FromException<int>(Transient()),
            cts.Token));
    }

    [Fact]
    public async Task BackoffStaysWithinDocumentedBounds()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays, () => 1.0); // maximum jitter

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<TemporaryDownloadException>(() => coordinator.ExecuteAsync<int>(
                RetryCategory.TemporaryDownload,
                _ => Task.FromException<int>(Transient()),
                CancellationToken.None));
        }

        Assert.All(delays, delay =>
        {
            Assert.True(delay >= TimeSpan.FromSeconds(1), $"Delay {delay} below minimum.");
            Assert.True(delay <= TimeSpan.FromSeconds(17), $"Delay {delay} above maximum plus jitter.");
        });
    }

    [Fact]
    public async Task GraphCategoryIsRejected()
    {
        var coordinator = Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.ExecuteAsync(
            RetryCategory.GraphMetadata,
            _ => Task.FromResult(1),
            CancellationToken.None));
    }
}
