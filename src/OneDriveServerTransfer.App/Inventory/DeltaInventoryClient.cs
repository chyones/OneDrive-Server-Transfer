using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Inventory;

/// <summary>
/// Page-by-page drive delta inventory client (GRAPH-SCAN-001). Pages stream to a
/// caller-provided sink so the complete drive hierarchy is never materialized in
/// memory; the sink runs (and persists the page plus the opaque paging link
/// transactionally) before the next page is requested. Paging links are followed as
/// opaque values, retries are owned exclusively by <see cref="GraphRequestChannel" />
/// and its <c>IRetryCoordinator</c>, and every request carries the channel's unique
/// client-request-id. URLs, tokens, and raw responses are never logged.
/// </summary>
/// <remarks>
/// The M1 <c>IGraphMetadataClient</c> seam is intentionally not used: its
/// IAsyncEnumerable shape cannot expose page boundaries (needed for atomic page and
/// checkpoint persistence), the resumable next link, the delta checkpoint, or the
/// 410 reset location. Item metadata re-read (GRAPH-ITEM-001) remains a later-slice
/// seam on <c>IGraphMetadataClient</c>.
/// </remarks>
public interface IDeltaInventoryClient
{
    /// <summary>
    /// Enumerates the drive from the root delta endpoint, or from
    /// <paramref name="resumeLink" /> when a previously persisted opaque next link
    /// exists. Invokes <paramref name="pageSink" /> once per page in order and awaits
    /// it before requesting the next page, so a crash can never skip an unapplied page.
    /// Returns the opaque delta checkpoint of the completed enumeration.
    /// Throws <see cref="DeltaCheckpointResetException" /> when Microsoft invalidates
    /// the checkpoint with a supported 410 reset response.
    /// </summary>
    Task<DeltaEnumerationResult> EnumerateAsync(
        string driveId,
        string? resumeLink,
        Func<DeltaInventoryPage, CancellationToken, Task> pageSink,
        CancellationToken cancellationToken);
}

public sealed class DeltaInventoryClient : IDeltaInventoryClient
{
    private const string InitialPageTemplate = "/drives/{drive-id}/root/delta";
    private const string OpaqueLinkTemplate = "{opaque paging link}";

    private readonly IGraphRequestChannel _channel;
    private readonly ILogger<DeltaInventoryClient> _logger;

    public DeltaInventoryClient(IGraphRequestChannel channel, ILogger<DeltaInventoryClient> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeltaEnumerationResult> EnumerateAsync(
        string driveId,
        string? resumeLink,
        Func<DeltaInventoryPage, CancellationToken, Task> pageSink,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driveId);
        ArgumentNullException.ThrowIfNull(pageSink);

        var requestUri = resumeLink is not null
            ? new Uri(resumeLink)
            : new Uri(string.Format(GraphEndpoints.DriveRootDeltaTemplate, Uri.EscapeDataString(driveId)));
        var template = resumeLink is not null ? OpaqueLinkTemplate : InitialPageTemplate;

        long pageCount = 0;
        long itemCount = 0;

        while (true)
        {
            DeltaInventoryPage page;
            try
            {
                using var document = await _channel
                    .GetJsonAsync(requestUri, template, cancellationToken)
                    .ConfigureAwait(false);
                page = GraphDeltaResponseParser.ParseDeltaPage(document);
            }
            catch (GraphRequestException exception) when (
                exception.StatusCode == 410 && exception.ResetLocation is not null)
            {
                // Supported delta reset (GRAPH-DELTA-003): classify and propagate the
                // opaque fresh-enumeration location. Never treated as state corruption.
                _logger.LogWarning(
                    "Delta checkpoint was invalidated by the service; reset required; pagesApplied={PageCount}",
                    pageCount);
                throw new DeltaCheckpointResetException(exception.ResetLocation);
            }

            pageCount++;
            itemCount += page.Items.Count;

            // The sink applies the page and persists the paging link transactionally
            // before the next request is made.
            await pageSink(page, cancellationToken).ConfigureAwait(false);

            if (page.IsFinal)
            {
                _logger.LogInformation(
                    "Delta enumeration completed; pages={PageCount}; items={ItemCount}; resumed={Resumed}",
                    pageCount, itemCount, resumeLink is not null);
                return new DeltaEnumerationResult(page.DeltaLink!, pageCount, itemCount);
            }

            requestUri = new Uri(page.NextLink!);
            template = OpaqueLinkTemplate;
        }
    }
}
