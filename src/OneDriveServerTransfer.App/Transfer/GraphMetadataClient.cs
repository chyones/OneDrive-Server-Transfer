using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Read-only Graph metadata client for the transfer pipeline (GRAPH-ITEM-001 and
/// GRAPH-DL-001, plus the <see cref="IGraphMetadataClient" /> delta seam). Every
/// request flows through <see cref="IGraphRequestChannel" />, so authentication,
/// correlation, and Graph retries stay with the Graph layer; temporary download URLs
/// are returned to the caller immediately and are never logged or persisted here.
/// </summary>
public sealed class GraphMetadataClient : IGraphMetadataClient
{
    private const string ItemTemplate = "/drives/{drive-id}/items/{item-id}";

    private readonly IGraphRequestChannel _channel;
    private readonly IDeltaInventoryClient _deltaInventoryClient;
    private readonly ILogger<GraphMetadataClient> _logger;

    public GraphMetadataClient(
        IGraphRequestChannel channel,
        IDeltaInventoryClient deltaInventoryClient,
        ILogger<GraphMetadataClient> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _deltaInventoryClient = deltaInventoryClient ?? throw new ArgumentNullException(nameof(deltaInventoryClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<DriveItemMetadata> EnumerateDriveDeltaAsync(
        string driveId,
        string? deltaCheckpoint,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = Channel.CreateUnbounded<DeltaInventoryItem>();
        var enumeration = Task.Run(async () =>
        {
            try
            {
                await _deltaInventoryClient.EnumerateAsync(
                        driveId,
                        deltaCheckpoint,
                        (page, pageCt) =>
                        {
                            foreach (var item in page.Items)
                            {
                                queue.Writer.TryWrite(item);
                            }

                            return Task.CompletedTask;
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                queue.Writer.Complete();
            }
            catch (Exception exception)
            {
                queue.Writer.Complete(exception);
            }
        }, CancellationToken.None);

        await foreach (var item in queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ToMetadata(item);
        }

        await enumeration.ConfigureAwait(false);
    }

    public async Task<DriveItemMetadata> GetItemMetadataAsync(
        string driveId,
        string itemId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var requestUri = new Uri(string.Format(
            GraphEndpoints.DriveItemTemplate,
            Uri.EscapeDataString(driveId),
            Uri.EscapeDataString(itemId)));

        using var document = await _channel
            .GetJsonAsync(requestUri, ItemTemplate, cancellationToken)
            .ConfigureAwait(false);

        return ToMetadata(GraphDeltaResponseParser.ParseItem(document.RootElement));
    }

    public async Task<Uri> GetTemporaryDownloadUrlAsync(
        string driveId,
        string itemId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var requestUri = new Uri(string.Format(
            GraphEndpoints.DriveItemDownloadUrlTemplate,
            Uri.EscapeDataString(driveId),
            Uri.EscapeDataString(itemId)));

        using var document = await _channel
            .GetJsonAsync(requestUri, ItemTemplate, cancellationToken)
            .ConfigureAwait(false);

        string? url = null;
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("@microsoft.graph.downloadUrl", out var urlElement) &&
            urlElement.ValueKind == JsonValueKind.String)
        {
            url = urlElement.GetString();
        }

        // Only the presence of the URL is logged, never the URL itself.
        _logger.LogDebug("Temporary download URL obtained; itemRef={ItemReference}", itemId);

        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var temporaryUrl))
        {
            throw new GraphRequestException(
                200, graphErrorCode: null, isTransient: false, retryAfter: null,
                errorHintForClassification: "The metadata response carried no usable download URL.");
        }

        return temporaryUrl;
    }

    private static DriveItemMetadata ToMetadata(DeltaInventoryItem item) => new(
        item.ItemId,
        item.ParentItemId,
        item.Name,
        item.SizeBytes,
        item.Facet == DeltaItemFacet.Folder,
        item.Facet == DeltaItemFacet.Package,
        item.Facet == DeltaItemFacet.Deleted,
        item.ETag,
        item.CTag,
        item.CreatedUtc,
        item.LastModifiedUtc,
        item.SourceHashAlgorithm,
        item.SourceHashValue);
}
