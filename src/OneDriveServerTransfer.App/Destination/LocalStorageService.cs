using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// M1 local-storage seam, implemented in M4 over the destination services. Validation
/// failures are reported as <see cref="DestinationValidationResult" /> carrying the
/// stable reference code; lock acquisition failures throw the reference-coded
/// <see cref="DestinationException" /> (<c>DST-LOCK-001</c>).
/// </summary>
public sealed class LocalStorageService : ILocalStorageService
{
    private readonly IDestinationValidator _validator;
    private readonly IDestinationLayoutService _layout;
    private readonly IDestinationLockService _lockService;
    private readonly IDestinationCapacityService _capacityService;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(
        IDestinationValidator validator,
        IDestinationLayoutService layout,
        IDestinationLockService lockService,
        IDestinationCapacityService capacityService,
        ILogger<LocalStorageService> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _capacityService = capacityService ?? throw new ArgumentNullException(nameof(capacityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DestinationValidationResult> ValidateDestinationAsync(
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _validator.ValidateAndCanonicalize(destinationRoot);
            return Task.FromResult(new DestinationValidationResult(true, null));
        }
        catch (DestinationException exception)
        {
            _logger.LogInformation(
                "Destination validation failed; code={ReferenceCode}", exception.ReferenceCode);
            return Task.FromResult(new DestinationValidationResult(false, exception.ReferenceCode));
        }
    }

    public Task<IDestinationExclusiveLock> AcquireExclusiveLockAsync(
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var canonicalRoot = _validator.ValidateAndCanonicalize(destinationRoot);
        var destination = _layout.EnsureLayout(canonicalRoot);
        IDestinationLock destinationLock = _lockService.Acquire(destination);
        return Task.FromResult<IDestinationExclusiveLock>(destinationLock);
    }

    public long GetFreeSpaceBytes(string destinationRoot)
    {
        var canonicalRoot = _validator.ValidateAndCanonicalize(destinationRoot);
        return _capacityService.GetAvailableFreeSpaceBytes(canonicalRoot);
    }
}
