using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests.SourceResolution;

public class GraphRetryCoordinatorTests
{
    private static GraphRetryCoordinator Create(IList<TimeSpan>? delays = null, Func<double>? jitter = null) =>
        new(
            NullLogger<GraphRetryCoordinator>.Instance,
            (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            },
            jitter ?? (() => 0.0));

    [Fact]
    public async Task SuccessOnFirstAttemptDoesNotRetry()
    {
        var coordinator = Create();
        var attempts = 0;

        var result = await coordinator.ExecuteAsync(
            RetryCategory.GraphMetadata,
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
            RetryCategory.GraphMetadata,
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(new GraphRequestException(503, "serviceUnavailable", true, null, null))
                    : Task.FromResult(7);
            },
            CancellationToken.None);

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(1), delays[0]);
    }

    [Fact]
    public async Task RetryAfterHeaderIsHonoredOverBackoff()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays);
        var attempts = 0;

        await coordinator.ExecuteAsync<int>(
            RetryCategory.GraphMetadata,
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(new GraphRequestException(429, "tooManyRequests", true, TimeSpan.FromSeconds(5), null))
                    : Task.FromResult(1);
            },
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(delays));
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        var coordinator = Create();
        var attempts = 0;

        await Assert.ThrowsAsync<GraphRequestException>(() => coordinator.ExecuteAsync<int>(
            RetryCategory.GraphMetadata,
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new GraphRequestException(404, "itemNotFound", false, null, null));
            },
            CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task BudgetIsBoundedAtMaxAttempts()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() => coordinator.ExecuteAsync<int>(
            RetryCategory.GraphMetadata,
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new GraphRequestException(429, "tooManyRequests", true, TimeSpan.FromSeconds(2), null));
            },
            CancellationToken.None));

        Assert.Equal(GraphRetryCoordinator.MaxAttempts, attempts);
        Assert.Equal(GraphRetryCoordinator.MaxAttempts - 1, delays.Count);
        Assert.Equal(429, exception.StatusCode);
    }

    [Fact]
    public async Task CancellationDuringDelayStopsPromptly()
    {
        using var cts = new CancellationTokenSource();
        var coordinator = new GraphRetryCoordinator(
            NullLogger<GraphRetryCoordinator>.Instance,
            async (delay, ct) =>
            {
                cts.Cancel();
                await Task.Delay(delay, ct);
            },
            () => 0.0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ExecuteAsync<int>(
            RetryCategory.GraphMetadata,
            _ => Task.FromException<int>(new GraphRequestException(503, null, true, null, null)),
            cts.Token));
    }

    [Fact]
    public async Task BackoffStaysWithinDocumentedBounds()
    {
        var delays = new List<TimeSpan>();
        var coordinator = Create(delays, () => 1.0); // maximum jitter

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<GraphRequestException>(() => coordinator.ExecuteAsync<int>(
                RetryCategory.GraphMetadata,
                _ => Task.FromException<int>(new GraphRequestException(503, null, true, null, null)),
                CancellationToken.None));
        }

        Assert.All(delays, delay =>
        {
            Assert.True(delay >= TimeSpan.FromSeconds(1), $"Delay {delay} below minimum.");
            Assert.True(delay <= TimeSpan.FromSeconds(9), $"Delay {delay} above maximum plus jitter.");
        });
    }

    [Fact]
    public async Task NonGraphCategoryIsRejected()
    {
        var coordinator = Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.ExecuteAsync(
            RetryCategory.TemporaryDownload,
            _ => Task.FromResult(1),
            CancellationToken.None));
    }
}
