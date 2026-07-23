using System.Text.Json;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Inventory;

/// <summary>
/// Verifies page-by-page delta enumeration: opaque-link following and preservation,
/// sink ordering (each page delivered before the next request), the returned
/// checkpoint, resume links, empty drives, and 410 reset classification.
/// </summary>
public class DeltaInventoryClientTests
{
    private static DeltaInventoryClient CreateClient(FakeGraphRequestChannel channel) =>
        new(channel, new CapturingLogger<DeltaInventoryClient>());

    private static JsonDocument Page(string itemsJson, string? nextLink, string? deltaLink)
    {
        var links = nextLink is not null
            ? $"""
              "@odata.nextLink": "{nextLink}"
              """
            : $"""
              "@odata.deltaLink": "{deltaLink}"
              """;
        return JsonDocument.Parse($$"""{ "value": [{{itemsJson}}], {{links}} }""");
    }

    [Fact]
    public async Task FollowsNextLinksUntilDeltaLinkAndReturnsOpaqueCheckpoint()
    {
        var channel = new FakeGraphRequestChannel();
        var responses = new Queue<JsonDocument>(
        [
            Page("""{ "id": "root", "folder": {} }""", "https://opaque/next?page=2", null),
            Page("""{ "id": "f1", "parentReference": { "id": "root" }, "file": {} }""",
                "https://opaque/next?page=3", null),
            Page("", null, "https://opaque/delta?token=abc"),
        ]);
        channel.Handler = (_, _, _) => Task.FromResult(responses.Dequeue());

        var pages = new List<DeltaInventoryPage>();
        var result = await CreateClient(channel).EnumerateAsync(
            "drive-1", null,
            (page, _) =>
            {
                pages.Add(page);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(3, result.PageCount);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal("https://opaque/delta?token=abc", result.DeltaCheckpoint);

        // The first request uses the approved template with the documented $select and
        // never selects the temporary download URL property.
        var initialUri = channel.Requests[0].Uri.ToString();
        Assert.Contains("/drives/drive-1/root/delta?$select=", initialUri);
        Assert.Contains("file,folder,package,deleted", initialUri);
        Assert.DoesNotContain("download", initialUri, StringComparison.OrdinalIgnoreCase);

        // Opaque links are followed exactly as returned, never parsed or modified.
        Assert.Equal("https://opaque/next?page=2", channel.Requests[1].Uri.ToString());
        Assert.Equal("https://opaque/next?page=3", channel.Requests[2].Uri.ToString());
        Assert.Equal(3, pages.Count);
        Assert.Equal("https://opaque/delta?token=abc", pages[2].DeltaLink);
    }

    [Fact]
    public async Task DeliversEachPageToTheSinkBeforeRequestingTheNext()
    {
        var channel = new FakeGraphRequestChannel();
        var sinkAppliedPages = 0;
        channel.Handler = (uri, _, _) =>
        {
            // If the next page were requested before the sink completed, this would
            // observe a count higher than the page being served.
            var pageIndex = channel.Requests.Count - 1;
            Assert.Equal(pageIndex, sinkAppliedPages);
            return Task.FromResult(pageIndex == 0
                ? Page("""{ "id": "a" }""", "https://opaque/next", null)
                : Page("", null, "https://opaque/delta"));
        };

        await CreateClient(channel).EnumerateAsync(
            "drive-1", null,
            (_, _) =>
            {
                sinkAppliedPages++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(2, sinkAppliedPages);
    }

    [Fact]
    public async Task ResumesFromThePersistedOpaqueLinkWithoutTouchingTheRootEndpoint()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(Page("", null, "https://opaque/delta?token=resumed"))
        };

        var result = await CreateClient(channel).EnumerateAsync(
            "drive-1", "https://opaque/next?token=saved",
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        var request = Assert.Single(channel.Requests);
        Assert.Equal("https://opaque/next?token=saved", request.Uri.ToString());
        Assert.Equal("https://opaque/delta?token=resumed", result.DeltaCheckpoint);
    }

    [Fact]
    public async Task EmptyDriveReturnsCheckpointWithZeroItems()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(Page("", null, "https://opaque/delta?token=empty"))
        };

        var result = await CreateClient(channel).EnumerateAsync(
            "drive-1", null, (_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(1, result.PageCount);
        Assert.Equal(0, result.ItemCount);
        Assert.Equal("https://opaque/delta?token=empty", result.DeltaCheckpoint);
    }

    [Fact]
    public async Task ResetResponsePropagatesTheOpaqueFreshEnumerationLocation()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => throw new GraphRequestException(
                410, "resyncRequired", isTransient: false, retryAfter: null,
                errorHintForClassification: null,
                resetLocation: new Uri("https://opaque/fresh-enumeration"))
        };

        var exception = await Assert.ThrowsAsync<DeltaCheckpointResetException>(() =>
            CreateClient(channel).EnumerateAsync(
                "drive-1", "https://opaque/next?token=stale",
                (_, _) => Task.CompletedTask,
                CancellationToken.None));

        Assert.Equal(new Uri("https://opaque/fresh-enumeration"), exception.FreshEnumerationLocation);
    }

    [Fact]
    public async Task MalformedPageFailsSafelyAndStopsEnumeration()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(JsonDocument.Parse("""{ "value": [] }"""))
        };

        await Assert.ThrowsAsync<InventoryException>(() =>
            CreateClient(channel).EnumerateAsync(
                "drive-1", null, (_, _) => Task.CompletedTask, CancellationToken.None));

        // No further request may be issued after a protocol failure.
        Assert.Single(channel.Requests);
    }

    [Fact]
    public async Task GraphFailuresOtherThanResetPropagateUnchanged()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => throw new GraphRequestException(
                403, "accessDenied", isTransient: false, retryAfter: null, errorHintForClassification: null)
        };

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() =>
            CreateClient(channel).EnumerateAsync(
                "drive-1", null, (_, _) => Task.CompletedTask, CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task LogsNeverContainUrlsOrLinkValues()
    {
        var logger = new CapturingLogger<DeltaInventoryClient>();
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(Page("", null, "https://opaque/delta?token=secret"))
        };

        await new DeltaInventoryClient(channel, logger).EnumerateAsync(
            "drive-1", null, (_, _) => Task.CompletedTask, CancellationToken.None);

        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain("https://", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token=secret", message, StringComparison.Ordinal);
        }
    }
}
