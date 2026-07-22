using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;

namespace OneDriveServerTransfer.Tests.Transfer;

/// <summary>
/// Verifies the Graph metadata client: GRAPH-ITEM-001 item re-read parsing,
/// GRAPH-DL-001 temporary URL acquisition without URL logging, supported-hash
/// selection (D-038), and the delta enumeration seam.
/// </summary>
public class GraphMetadataClientTests
{
    private const string DriveId = "drive-1";
    private const string ItemId = "item-1";

    private static GraphMetadataClient Create(
        FakeGraphRequestChannel channel,
        CapturingLogger<GraphMetadataClient>? logger = null) =>
        new(channel, new FakeDeltaInventoryClient(),
            logger ?? new CapturingLogger<GraphMetadataClient>());

    [Fact]
    public async Task ItemReReadUsesApprovedEndpointAndParsesMetadata()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (uri, template, _) => Task.FromResult(FakeGraphRequestChannel.Json("""
                {
                  "id": "item-1",
                  "name": "report.xlsx",
                  "parentReference": { "id": "root" },
                  "size": 12345,
                  "eTag": "etag-1",
                  "cTag": "ctag-1",
                  "createdDateTime": "2026-01-02T03:04:05Z",
                  "lastModifiedDateTime": "2026-02-03T04:05:06Z",
                  "file": { "hashes": { "sha1Hash": "SHA1", "quickXorHash": "QXOR", "sha256Hash": "IGNORED" } }
                }
                """)),
        };
        var client = Create(channel);

        var metadata = await client.GetItemMetadataAsync(DriveId, ItemId, CancellationToken.None);

        var request = Assert.Single(channel.Requests);
        Assert.StartsWith(GraphEndpoints.V1Base + "/drives/" + DriveId + "/items/" + ItemId, request.Uri.OriginalString);
        Assert.Contains("$select=", request.Uri.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("beta", request.Uri.OriginalString, StringComparison.Ordinal);

        Assert.Equal(ItemId, metadata.ItemId);
        Assert.Equal("root", metadata.ParentItemId);
        Assert.Equal("report.xlsx", metadata.Name);
        Assert.Equal(12345, metadata.SizeBytes);
        Assert.False(metadata.IsFolder);
        Assert.False(metadata.IsPackage);
        Assert.False(metadata.IsDeleted);
        Assert.Equal("etag-1", metadata.ETag);
        Assert.Equal("ctag-1", metadata.CTag);
        // quickXorHash is preferred when available; the Graph sha256Hash is ignored.
        Assert.Equal("quickXorHash", metadata.SourceHashAlgorithm);
        Assert.Equal("QXOR", metadata.SourceHashValue);
    }

    [Fact]
    public async Task TemporaryDownloadUrlIsReturnedButNeverLogged()
    {
        const string secretUrl = "https://download.example.test/preauth-secret";
        var channel = new FakeGraphRequestChannel
        {
            Handler = (uri, template, _) => Task.FromResult(FakeGraphRequestChannel.Json($$"""
                { "id": "item-1", "@microsoft.graph.downloadUrl": "{{secretUrl}}" }
                """)),
        };
        var logger = new CapturingLogger<GraphMetadataClient>();
        var client = Create(channel, logger);

        var url = await client.GetTemporaryDownloadUrlAsync(DriveId, ItemId, CancellationToken.None);

        Assert.Equal(new Uri(secretUrl), url);
        var request = Assert.Single(channel.Requests);
        Assert.Contains("%40microsoft.graph.downloadUrl", request.Uri.OriginalString, StringComparison.Ordinal);
        Assert.All(logger.Messages, message =>
            Assert.DoesNotContain("preauth-secret", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingDownloadUrlFailsWithoutRetryClassification()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (uri, template, _) => Task.FromResult(FakeGraphRequestChannel.Json("""{ "id": "item-1" }""")),
        };
        var client = Create(channel);

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() =>
            client.GetTemporaryDownloadUrlAsync(DriveId, ItemId, CancellationToken.None));
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task DeltaEnumerationYieldsConvertedMetadata()
    {
        var delta = new FakeDeltaInventoryClient();
        delta.EnqueuePage(new DeltaInventoryPage(
            [
                new DeltaInventoryItem("item-1", "root", "file.txt", 10, DeltaItemFacet.File,
                    "e1", "c1", null, null, "quickXorHash", "QX"),
                new DeltaInventoryItem("folder-1", "root", "folder", null, DeltaItemFacet.Folder,
                    null, null, null, null, null, null),
                new DeltaInventoryItem("pkg-1", "root", "notebook", null, DeltaItemFacet.Package,
                    null, null, null, null, null, null),
            ],
            NextLink: null, DeltaLink: "opaque-delta"));
        var client = new GraphMetadataClient(
            new FakeGraphRequestChannel(), delta, NullLogger<GraphMetadataClient>.Instance);

        var items = new List<Abstractions.DriveItemMetadata>();
        await foreach (var item in client.EnumerateDriveDeltaAsync(DriveId, null, CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.False(items[0].IsFolder);
        Assert.True(items[1].IsFolder);
        Assert.True(items[2].IsPackage);
        Assert.Equal("quickXorHash", items[0].SourceHashAlgorithm);
    }
}
