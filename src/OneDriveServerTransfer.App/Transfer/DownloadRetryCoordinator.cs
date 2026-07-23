using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// The single owner of automatic retries for temporary download-host requests
/// (docs/GRAPH_RESILIENCY_POLICY.md). Honors Retry-After, uses bounded exponential
/// backoff with jitter, allows at most five attempts per file (the binding per-file
/// budget), keeps cancellation responsive during delays, and never retries permanent
/// failures. Graph metadata calls (fresh URL acquisition, item re-read) keep their own
/// retry owner; this coordinator never wraps a Graph request, so no request ever has
/// two retry layers. Delays and jitter are injectable for deterministic tests and are
/// absent from the UI and configuration.
/// </summary>
public sealed class DownloadRetryCoordinator : IRetryCoordinator
{
    /// <summary>Maximum attempts per file, including the first (binding contract).</summary>
    public const int MaxAttempts = 5;

    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromSeconds(16);

    private readonly ILogger<DownloadRetryCoordinator> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<double> _jitterFraction;

    public DownloadRetryCoordinator(ILogger<DownloadRetryCoordinator> logger)
        : this(logger, null, null)
    {
    }

    /// <summary>Test constructor with deterministic delay and jitter.</summary>
    internal DownloadRetryCoordinator(
        ILogger<DownloadRetryCoordinator> logger,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        Func<double>? jitterFraction)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _delayAsync = delayAsync ?? ((delay, ct) => Task.Delay(delay, ct));
        _jitterFraction = jitterFraction ?? (() => Random.Shared.NextDouble());
    }

    public async Task<T> ExecuteAsync<T>(
        RetryCategory category,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (category != RetryCategory.TemporaryDownload)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category), category, "This coordinator owns temporary download requests only.");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TemporaryDownloadException exception) when (exception.IsRetryable && attempt < MaxAttempts)
            {
                var delay = exception.RetryAfter ?? ComputeBackoff(attempt);
                _logger.LogWarning(
                    "Download attempt failed; kind={Kind}; status={Status}; attempt={Attempt}/{MaxAttempts}; delaySeconds={Delay}",
                    exception.Kind,
                    exception.StatusCode?.ToString() ?? "n/a",
                    attempt,
                    MaxAttempts,
                    delay.TotalSeconds);
                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var baseMilliseconds = Math.Min(
            MinimumDelay.TotalMilliseconds * Math.Pow(2, attempt - 1),
            MaximumDelay.TotalMilliseconds);
        var jitterMilliseconds = _jitterFraction() * MinimumDelay.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(baseMilliseconds + jitterMilliseconds);
    }
}
