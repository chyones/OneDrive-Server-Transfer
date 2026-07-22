using Microsoft.Data.Sqlite;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Scan;

/// <summary>
/// Verifies the mandatory dry-run scan: happy-path counts and known bytes, unsupported
/// package collection, tombstone recording, deterministic mapping through the persisted
/// registry, crash resume from the persisted next link, 410 reset classification,
/// storage and permission warnings, and scan invalidation on source or destination
/// change.
/// </summary>
public class ScanServiceTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string EmployeeId = "employee-1";
    private const string DriveId = "drive-1";

    private readonly string _rootPath =
        Path.Combine(Path.GetTempPath(), $"odst-m5-scan-{Guid.NewGuid():N}");

    private sealed class Environment : IDisposable
    {
        public required ResolvedDestination Destination { get; init; }
        public required DestinationSession Session { get; init; }
        public required SqliteTransferStateStore StateStore { get; init; }
        public required FakeDeltaInventoryClient DeltaClient { get; init; }
        public required FakeAuthenticationService Authentication { get; init; }
        public required FakeDestinationBindingService BindingService { get; init; }
        public required FakeDriveSpaceProvider SpaceProvider { get; init; }
        public required FakeDestinationSecurityEvaluator SecurityEvaluator { get; init; }
        public required ScanService ScanService { get; init; }

        public void Dispose() => Session.Dispose();
    }

    private async Task<Environment> CreateEnvironmentAsync()
    {
        var destination = new ResolvedDestination(_rootPath);

        var bindingStore = new SqliteDestinationBindingStore(
            new SqliteTransferStateSchemaInitializer(),
            new CapturingLogger<SqliteDestinationBindingStore>());
        await bindingStore.CreateBindingAsync(
            destination.StateDatabasePath,
            new StoredDestinationBinding(
                TenantId, DriveId, EmployeeId, "employee@example.test",
                "operator-1", "operator@example.test", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var schemaInitializer = new SqliteTransferStateSchemaInitializer();
        var stateStore = new SqliteTransferStateStore(
            schemaInitializer, new CapturingLogger<SqliteTransferStateStore>());
        var registry = new SqlitePathCollisionRegistry(schemaInitializer);
        var spaceProvider = new FakeDriveSpaceProvider { AvailableFreeSpaceBytes = 1L << 40 };
        var securityEvaluator = new FakeDestinationSecurityEvaluator();
        var deltaClient = new FakeDeltaInventoryClient();
        var bindingService = new FakeDestinationBindingService();
        var authentication = new FakeAuthenticationService();
        authentication.SetSignedInOperator(new Abstractions.OperatorIdentity(
            "operator-1", "operator@example.test", "Operator", TenantId));

        var session = DestinationSessionFactory.Create(_rootPath);
        var scanService = new ScanService(
            authentication,
            bindingService,
            deltaClient,
            stateStore,
            registry,
            new PathMapperV1(registry),
            new DestinationCapacityService(spaceProvider),
            securityEvaluator,
            new CapturingLogger<ScanService>());

        return new Environment
        {
            Destination = destination,
            Session = session,
            StateStore = stateStore,
            DeltaClient = deltaClient,
            Authentication = authentication,
            BindingService = bindingService,
            SpaceProvider = spaceProvider,
            SecurityEvaluator = securityEvaluator,
            ScanService = scanService,
        };
    }

    private static ResolvedEmployeeSource Source(
        string tenantId = TenantId, string employeeId = EmployeeId, string driveId = DriveId) =>
        new(tenantId, employeeId, "employee@example.test", "Employee", driveId,
            "business", null, "https://example.invalid/", null, null, null,
            EmployeeSourceMode.Upn, IsTenantConfirmed: true);

    private static DeltaInventoryItem GraphItem(
        string id,
        string? parentId,
        string name,
        DeltaItemFacet facet,
        long? size = null) =>
        new(id, parentId, name, size, facet, $"etag-{id}", null,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            facet == DeltaItemFacet.File ? "quickXorHash" : null,
            facet == DeltaItemFacet.File ? "hash" : null);

    private static void EnqueueStandardInventory(FakeDeltaInventoryClient client)
    {
        client.EnqueuePage(new DeltaInventoryPage(
            [
                GraphItem("root", null, "root", DeltaItemFacet.Folder),
                GraphItem("folderA", "root", "FolderA", DeltaItemFacet.Folder),
                GraphItem("fileA1", "folderA", "a.txt", DeltaItemFacet.File, size: 100),
                GraphItem("notebook", "folderA", "Notebook", DeltaItemFacet.Package),
                GraphItem("gone", "root", "old.txt", DeltaItemFacet.Deleted),
            ],
            "https://opaque/next?page=2", null));
        client.EnqueuePage(new DeltaInventoryPage(
            [
                GraphItem("fileB", "root", "b.txt", DeltaItemFacet.File, size: 50),
                GraphItem("empty", "root", "Empty", DeltaItemFacet.Folder),
            ],
            null, "https://opaque/delta?token=1"));
    }

    [Fact]
    public async Task HappyPathProducesAccuratePreflightAndCurrentScan()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);

        var result = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        Assert.Equal(2, result.FileCount);
        Assert.Equal(150, result.KnownSourceBytes);
        Assert.Equal(2, result.FolderCount);
        Assert.Equal(1, result.EmptyFolderCount);
        Assert.Equal(1, result.UnsupportedCount);
        Assert.Empty(result.Warnings);

        var unsupported = Assert.Single(result.UnsupportedItems);
        Assert.Equal("Notebook", unsupported.Name);
        Assert.Equal("FolderA/Notebook", unsupported.SourcePath);
        Assert.Equal(nameof(ItemFacetClassification.UnsupportedPackage), unsupported.Classification);

        // The scan revalidated the operator and the destination binding.
        Assert.Equal(1, environment.BindingService.CallCount);
        Assert.Equal(DriveId, environment.BindingService.LastSource!.DriveId);

        // A successful scan is current for the same source and destination.
        Assert.True(await environment.ScanService.IsScanCurrentAsync(
            Source(), environment.Session, CancellationToken.None));
    }

    [Fact]
    public async Task HappyPathPersistsClassifiedInventoryMappingsAndCheckpoint()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);

        await environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None);

        var mapped = await environment.StateStore.GetItemsByStateAsync(
            TransferItemState.Mapped, CancellationToken.None);
        Assert.Equal(4, mapped.Count);

        var file = await environment.StateStore.GetItemAsync("fileA1", CancellationToken.None);
        Assert.Equal("FolderA/a.txt", file!.SourcePath);
        Assert.Equal(@"FolderA\a.txt", file.MappedRelativePath);

        var empty = await environment.StateStore.GetItemAsync("empty", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.EmptyFolder, empty!.Classification);

        var notebook = await environment.StateStore.GetItemAsync("notebook", CancellationToken.None);
        Assert.Equal(TransferItemState.Unsupported, notebook!.TransferState);
        Assert.Null(notebook.MappedRelativePath);

        // The tombstone is recorded without local deletion semantics: no paths, no
        // copy state, and never counted as current content.
        var gone = await environment.StateStore.GetItemAsync("gone", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.DeletedSource, gone!.Classification);
        Assert.Null(gone.MappedRelativePath);

        var root = await environment.StateStore.GetItemAsync("root", CancellationToken.None);
        Assert.Equal(TransferItemState.Skipped, root!.TransferState);

        var checkpoint = await environment.StateStore.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal("https://opaque/delta?token=1", checkpoint!.Checkpoint);
        Assert.Equal(DeltaCheckpointState.DeltaCheckpointValid, checkpoint.State);
    }

    [Fact]
    public async Task EmptyDriveScanSucceedsWithZeroCounts()
    {
        using var environment = await CreateEnvironmentAsync();
        environment.DeltaClient.EnqueuePage(new DeltaInventoryPage(
            [GraphItem("root", null, "root", DeltaItemFacet.Folder)],
            null, "https://opaque/delta?token=empty"));

        var result = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.KnownSourceBytes);
        Assert.Equal(0, result.FolderCount);
        Assert.True(await environment.ScanService.IsScanCurrentAsync(
            Source(), environment.Session, CancellationToken.None));
    }

    [Fact]
    public async Task CrashedScanResumesFromThePersistedNextLink()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);
        environment.DeltaClient.FailAfterPages = 1;
        environment.DeltaClient.Failure = new GraphRequestException(
            503, null, isTransient: true, retryAfter: null, errorHintForClassification: null);

        var failure = await Assert.ThrowsAsync<ScanException>(() =>
            environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None));
        Assert.Equal(ScanErrorCodes.ServiceUnavailable, failure.ReferenceCode);

        // The crash never enables copying.
        Assert.False(await environment.ScanService.IsScanCurrentAsync(
            Source(), environment.Session, CancellationToken.None));

        // Second attempt: the client receives the persisted opaque next link and only
        // the remaining page is served.
        var retryClient = new FakeDeltaInventoryClient();
        retryClient.EnqueuePage(new DeltaInventoryPage(
            [GraphItem("fileB", "root", "b.txt", DeltaItemFacet.File, size: 50)],
            null, "https://opaque/delta?token=resumed"));
        var registry = new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer());
        var resumedService = new ScanService(
            environment.Authentication,
            environment.BindingService,
            retryClient,
            environment.StateStore,
            registry,
            new PathMapperV1(registry),
            new DestinationCapacityService(environment.SpaceProvider),
            environment.SecurityEvaluator,
            new CapturingLogger<ScanService>());

        var result = await resumedService.ScanAsync(Source(), environment.Session, CancellationToken.None);

        Assert.Equal("https://opaque/next?page=2", Assert.Single(retryClient.ResumeLinks));
        Assert.Equal(2, result.FileCount);
        Assert.Equal(150, result.KnownSourceBytes);
        Assert.True(await resumedService.IsScanCurrentAsync(
            Source(), environment.Session, CancellationToken.None));
    }

    [Fact]
    public async Task ResetResponseDuringResumeClassifiesAndRequiresFreshScan()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);
        environment.DeltaClient.FailAfterPages = 1;
        environment.DeltaClient.Failure = new GraphRequestException(
            503, null, isTransient: true, retryAfter: null, errorHintForClassification: null);
        await Assert.ThrowsAsync<ScanException>(() =>
            environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None));

        // The next enumeration attempt hits the supported 410 reset surface.
        var resetClient = new FakeDeltaInventoryClient
        {
            FailAfterPages = 0,
            Failure = new DeltaCheckpointResetException(new Uri("https://opaque/fresh")),
        };
        resetClient.EnqueuePage(new DeltaInventoryPage([], null, "https://opaque/delta?unused"));
        var registry = new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer());
        var resetService = new ScanService(
            environment.Authentication,
            environment.BindingService,
            resetClient,
            environment.StateStore,
            registry,
            new PathMapperV1(registry),
            new DestinationCapacityService(environment.SpaceProvider),
            environment.SecurityEvaluator,
            new CapturingLogger<ScanService>());

        var exception = await Assert.ThrowsAsync<ScanException>(() =>
            resetService.ScanAsync(Source(), environment.Session, CancellationToken.None));
        Assert.Equal(ScanErrorCodes.DeltaResetRequired, exception.ReferenceCode);

        // The prior checkpoint is preserved and the state records the reset; a 410 is
        // never treated as database corruption.
        var checkpoint = await environment.StateStore.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal(DeltaCheckpointState.DeltaCheckpointResetRequired, checkpoint!.State);
        Assert.Equal("https://opaque/next?page=2", checkpoint.Checkpoint);
        Assert.NotNull(await environment.StateStore.GetItemAsync("fileA1", CancellationToken.None));

        // The following scan starts a fresh enumeration from the root endpoint.
        var freshClient = new FakeDeltaInventoryClient();
        EnqueueStandardInventory(freshClient);
        var freshRegistry = new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer());
        var freshService = new ScanService(
            environment.Authentication,
            environment.BindingService,
            freshClient,
            environment.StateStore,
            freshRegistry,
            new PathMapperV1(freshRegistry),
            new DestinationCapacityService(environment.SpaceProvider),
            environment.SecurityEvaluator,
            new CapturingLogger<ScanService>());

        var result = await freshService.ScanAsync(Source(), environment.Session, CancellationToken.None);

        Assert.Null(Assert.Single(freshClient.ResumeLinks));
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public async Task StorageWarningAppearsExactlyAtTheReserveBoundary()
    {
        using var environment = await CreateEnvironmentAsync();

        // Boundary-exact free space fails: strictly greater is required.
        environment.SpaceProvider.AvailableFreeSpaceBytes = 150 + DestinationCapacityService.ReserveBytes;
        EnqueueStandardInventory(environment.DeltaClient);
        var tight = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);
        Assert.Contains(tight.Warnings,
            warning => warning.Kind == ScanWarningKind.InsufficientStorage);

        environment.SpaceProvider.AvailableFreeSpaceBytes =
            150 + DestinationCapacityService.ReserveBytes + 1;
        EnqueueStandardInventory(environment.DeltaClient);
        var comfortable = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);
        Assert.DoesNotContain(comfortable.Warnings,
            warning => warning.Kind == ScanWarningKind.InsufficientStorage);
    }

    [Fact]
    public async Task BroadDestinationPermissionsProduceAScanWarning()
    {
        using var environment = await CreateEnvironmentAsync();
        environment.SecurityEvaluator.Assessment = new DestinationSecurityAssessment(
            DestinationSecurityVerdict.BroadExposureWarning,
            [new DestinationSecurityFinding("path", "S-1-1-0", "ReadData")]);
        EnqueueStandardInventory(environment.DeltaClient);

        var result = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        Assert.Contains(result.Warnings,
            warning => warning.Kind == ScanWarningKind.BroadPermissionExposure);
    }

    [Fact]
    public async Task NameCollisionsProducePathWarningsAndDistinctDeterministicMappings()
    {
        using var environment = await CreateEnvironmentAsync();
        environment.DeltaClient.EnqueuePage(new DeltaInventoryPage(
            [
                GraphItem("root", null, "root", DeltaItemFacet.Folder),
                GraphItem("f1", "root", "same.txt", DeltaItemFacet.File, size: 1),
                GraphItem("f2", "root", "same.txt", DeltaItemFacet.File, size: 2),
            ],
            null, "https://opaque/delta?token=1"));

        var result = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Kind == ScanWarningKind.PathAdjusted);

        var first = await environment.StateStore.GetItemAsync("f1", CancellationToken.None);
        var second = await environment.StateStore.GetItemAsync("f2", CancellationToken.None);
        Assert.NotEqual(first!.MappedRelativePath, second!.MappedRelativePath);
        Assert.Equal(TransferItemState.Mapped, first.TransferState);
        Assert.Equal(TransferItemState.Mapped, second!.TransferState);

        // A re-scan reuses the persisted mappings: the mapped names are identical. The
        // collided item deterministically maps to its suffixed name again, which the
        // mapper reports with UsedCollisionSuffix (M4 semantics), so the adjustment
        // warning reappears rather than indicating a new collision.
        environment.DeltaClient.EnqueuePage(new DeltaInventoryPage(
            [
                GraphItem("root", null, "root", DeltaItemFacet.Folder),
                GraphItem("f1", "root", "same.txt", DeltaItemFacet.File, size: 1),
                GraphItem("f2", "root", "same.txt", DeltaItemFacet.File, size: 2),
            ],
            null, "https://opaque/delta?token=2"));
        await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        var firstAgain = await environment.StateStore.GetItemAsync("f1", CancellationToken.None);
        var secondAgain = await environment.StateStore.GetItemAsync("f2", CancellationToken.None);
        Assert.Equal(first.MappedRelativePath, firstAgain!.MappedRelativePath);
        Assert.Equal(second.MappedRelativePath, secondAgain!.MappedRelativePath);
    }

    [Fact]
    public async Task ExcessivelyLongPathsFailTheItemSafelyWithAPathWarning()
    {
        using var environment = await CreateEnvironmentAsync();
        var items = new List<DeltaInventoryItem>
        {
            GraphItem("root", null, "root", DeltaItemFacet.Folder)
        };
        var parent = "root";
        // 165 levels x 201 characters exceeds the 32767-unit canonical path limit.
        for (var level = 0; level < 165; level++)
        {
            var id = $"folder-{level}";
            items.Add(GraphItem(id, parent, new string('x', 200), DeltaItemFacet.Folder));
            parent = id;
        }

        environment.DeltaClient.EnqueuePage(new DeltaInventoryPage(
            items, null, "https://opaque/delta?token=1"));

        var result = await environment.ScanService.ScanAsync(
            Source(), environment.Session, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Kind == ScanWarningKind.PathFailure);
        var failed = await environment.StateStore.GetItemsByStateAsync(
            TransferItemState.Failed, CancellationToken.None);
        Assert.NotEmpty(failed);
        // The scan still succeeds: only the affected items fail.
        Assert.True(await environment.ScanService.IsScanCurrentAsync(
            Source(), environment.Session, CancellationToken.None));
    }

    [Fact]
    public async Task ScanIsInvalidatedByASourceChange()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);
        await environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None);

        Assert.False(await environment.ScanService.IsScanCurrentAsync(
            Source(driveId: "drive-other"), environment.Session, CancellationToken.None));
        Assert.False(await environment.ScanService.IsScanCurrentAsync(
            Source(employeeId: "employee-other"), environment.Session, CancellationToken.None));
        Assert.False(await environment.ScanService.IsScanCurrentAsync(
            Source(tenantId: "tenant-other"), environment.Session, CancellationToken.None));
    }

    [Fact]
    public async Task ScanIsInvalidatedByADestinationChange()
    {
        using var environment = await CreateEnvironmentAsync();
        EnqueueStandardInventory(environment.DeltaClient);
        await environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None);

        using var otherSession = DestinationSessionFactory.Create(Path.Combine(_rootPath, "other-dest"));
        Assert.False(await environment.ScanService.IsScanCurrentAsync(
            Source(), otherSession, CancellationToken.None));
    }

    [Fact]
    public async Task ScanRequiresASignedInOperator()
    {
        using var environment = await CreateEnvironmentAsync();
        await environment.Authentication.SignOutAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ScanException>(() =>
            environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None));
        Assert.Equal(ScanErrorCodes.OperatorSessionRequired, exception.ReferenceCode);
    }

    [Fact]
    public async Task ScanRejectsAnOperatorFromAnotherTenant()
    {
        using var environment = await CreateEnvironmentAsync();
        environment.Authentication.SetSignedInOperator(new Abstractions.OperatorIdentity(
            "operator-1", "operator@example.test", "Operator", "tenant-other"));

        var exception = await Assert.ThrowsAsync<ScanException>(() =>
            environment.ScanService.ScanAsync(Source(), environment.Session, CancellationToken.None));
        Assert.Equal(ScanErrorCodes.OperatorTenantMismatch, exception.ReferenceCode);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
