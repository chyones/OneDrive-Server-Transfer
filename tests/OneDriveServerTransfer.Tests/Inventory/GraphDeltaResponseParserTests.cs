using System.Text.Json;
using OneDriveServerTransfer.Inventory;

namespace OneDriveServerTransfer.Tests.Inventory;

/// <summary>
/// Verifies delta-page parsing: facet classification (including package, tombstone,
/// external shortcut, and unknown facets), tolerant handling of absent fields and
/// unknown JSON properties, supported source-hash selection, and protocol failures.
/// </summary>
public class GraphDeltaResponseParserTests
{
    private static JsonDocument Json(string json) => JsonDocument.Parse(json);

    [Fact]
    public void ParsesFileItemWithSupportedHashesAndTimes()
    {
        using var document = Json("""
            {
              "value": [
                {
                  "id": "item-1",
                  "name": "report.xlsx",
                  "parentReference": { "id": "root", "path": "/drive/root:" },
                  "size": 12345,
                  "eTag": "etag-1",
                  "cTag": "ctag-1",
                  "createdDateTime": "2026-01-02T03:04:05Z",
                  "lastModifiedDateTime": "2026-02-03T04:05:06Z",
                  "file": { "hashes": { "sha1Hash": "SHA1", "quickXorHash": "QXOR", "sha256Hash": "IGNORED" } },
                  "someFutureProperty": { "nested": true }
                }
              ],
              "@odata.deltaLink": "https://opaque.delta/link"
            }
            """);

        var page = GraphDeltaResponseParser.ParseDeltaPage(document);

        Assert.Equal("https://opaque.delta/link", page.DeltaLink);
        Assert.Null(page.NextLink);
        Assert.True(page.IsFinal);

        var item = Assert.Single(page.Items);
        Assert.Equal("item-1", item.ItemId);
        Assert.Equal("root", item.ParentItemId);
        Assert.Equal("report.xlsx", item.Name);
        Assert.Equal(12345, item.SizeBytes);
        Assert.Equal(DeltaItemFacet.File, item.Facet);
        Assert.Equal("etag-1", item.ETag);
        Assert.Equal("ctag-1", item.CTag);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), item.CreatedUtc);
        Assert.Equal(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero), item.LastModifiedUtc);
        // sha1Hash wins over quickXorHash; the Graph sha256Hash is never used (D-038).
        Assert.Equal("sha1Hash", item.SourceHashAlgorithm);
        Assert.Equal("SHA1", item.SourceHashValue);
    }

    [Fact]
    public void FallsBackToQuickXorHashWhenSha1Absent()
    {
        using var document = Json("""
            {
              "value": [ { "id": "f1", "file": { "hashes": { "quickXorHash": "QX" } } } ],
              "@odata.deltaLink": "d"
            }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal("quickXorHash", item.SourceHashAlgorithm);
        Assert.Equal("QX", item.SourceHashValue);
    }

    [Fact]
    public void MissingHashesStayNullWithoutFailure()
    {
        using var document = Json("""
            { "value": [ { "id": "f1", "file": { "mimeType": "text/plain" } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.File, item.Facet);
        Assert.Null(item.SourceHashAlgorithm);
        Assert.Null(item.SourceHashValue);
    }

    [Fact]
    public void ClassifiesFolderWithoutRequiringCTag()
    {
        using var document = Json("""
            { "value": [ { "id": "d1", "name": "Folder", "folder": { "childCount": 2 } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.Folder, item.Facet);
        Assert.Null(item.CTag);
    }

    [Fact]
    public void ClassifiesPackageAsUnsupported()
    {
        using var document = Json("""
            { "value": [ { "id": "n1", "name": "Notebook", "package": { "type": "oneNote" } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.Package, item.Facet);
    }

    [Fact]
    public void DeletedFacetWinsOverOtherFacets()
    {
        using var document = Json("""
            { "value": [ { "id": "gone", "name": "old.txt", "deleted": { "state": "deleted" }, "file": { } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.Deleted, item.Facet);
    }

    [Fact]
    public void ClassifiesRemoteItemAsExternalShortcut()
    {
        using var document = Json("""
            { "value": [ { "id": "s1", "name": "shared", "remoteItem": { "id": "other" }, "folder": { } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.ExternalShortcut, item.Facet);
    }

    [Fact]
    public void ItemWithoutAnyFacetClassifiesUnknownInsteadOfBeingDropped()
    {
        using var document = Json("""
            { "value": [ { "id": "mystery", "name": "???", "futureFacet": { } } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Equal(DeltaItemFacet.Unknown, item.Facet);
    }

    [Fact]
    public void MissingParentReferenceAndNameAreTolerated()
    {
        using var document = Json("""
            { "value": [ { "id": "root" } ], "@odata.deltaLink": "d" }
            """);

        var item = Assert.Single(GraphDeltaResponseParser.ParseDeltaPage(document).Items);
        Assert.Null(item.ParentItemId);
        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(DeltaItemFacet.Unknown, item.Facet);
    }

    [Fact]
    public void EmptyValueArrayIsValid()
    {
        using var document = Json("""{ "value": [], "@odata.nextLink": "n" }""");

        var page = GraphDeltaResponseParser.ParseDeltaPage(document);
        Assert.Empty(page.Items);
        Assert.Equal("n", page.NextLink);
    }

    [Theory]
    [InlineData("""{ "value": [] }""")]                                        // no paging link at all
    [InlineData("""{ "value": [], "@odata.nextLink": "n", "@odata.deltaLink": "d" }""")] // both links
    [InlineData("""{ "items": [] }""")]                                        // no value array
    [InlineData("""{ "value": [ { "name": "no id" } ], "@odata.deltaLink": "d" }""")]    // item without ID
    public void ProtocolFailuresThrowInventoryException(string json)
    {
        using var document = Json(json);

        var exception = Assert.Throws<InventoryException>(
            () => GraphDeltaResponseParser.ParseDeltaPage(document));
        Assert.Equal(InventoryErrorCodes.MalformedDeltaResponse, exception.ReferenceCode);
    }
}
