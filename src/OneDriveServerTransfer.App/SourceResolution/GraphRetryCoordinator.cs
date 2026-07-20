using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// The single owner of automatic retries for Microsoft Graph metadata requests
/// (decision D-036). Honors Retry-After, uses bounded exponential backoff with jitter,
/// keeps cancellation responsive during delays, and never retries permanent validation
/// or authorization failures. This build uses plain HTTP requests without a Graph SDK
/// retry handler, so no second retry layer exists.
/// </summary>
public sealed class GraphRetryCoordinator : IRetryCoordinator
{
    public const int MaxAttempts = 3;

    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromSeconds(8);

    private readonly ILogger<GraphRetryCoordinator> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<double> _jitterFraction;

    public GraphRetryCoordinator(ILogger<GraphRetryCoordinator> logger)
        : this(logger, null, null)
    {
    }

    /// <summary>Test constructor with deterministic delay and jitter.</summary>
    internal GraphRetryCoordinator(
        ILogger<GraphRetryCoordinator> logger,
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

        if (category != RetryCategory.GraphMetadata)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category), category, "This coordinator owns Graph metadata requests only.");
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
            catch (GraphRequestException exception) when (exception.IsTransient && attempt < MaxAttempts)
            {
                var delay = exception.RetryAfter ?? ComputeBackoff(attempt);
                _logger.LogWarning(
                    "Graph request failed transiently; status={Status}; code={Code}; attempt={Attempt}/{MaxAttempts}; delaySeconds={Delay}",
                    exception.StatusCode?.ToString() ?? "n/a",
                    exception.GraphErrorCode ?? "n/a",
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
