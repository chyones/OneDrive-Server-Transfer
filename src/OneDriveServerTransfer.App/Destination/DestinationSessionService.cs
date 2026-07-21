using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// An open destination session: validated selection, created layout, held exclusive
/// lock, and a validated source binding. Disposing the session releases the exclusive
/// lock. Milestone M5 consumes the session to run the scan and copy against
/// <see cref="Destination" />.
/// </summary>
public sealed class DestinationSession : IDisposable, IAsyncDisposable
{
    internal DestinationSession(
        ResolvedDestination destination, DestinationBindingResult binding, IDestinationLock destinationLock)
    {
        Destination = destination;
        Binding = binding;
        Lock = destinationLock;
    }

    public ResolvedDestination Destination { get; }

    public DestinationBindingResult Binding { get; }

    internal IDestinationLock Lock { get; }

    public void Dispose() => Lock.Dispose();

    public async ValueTask DisposeAsync() => await Lock.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// Opens a destination end to end: path validation, layout creation, exclusive-lock
/// acquisition, and source binding or same-source resume validation. The lock is taken
/// before any state write so two sessions can never bind the same destination
/// concurrently; when binding fails the lock is released immediately.
/// </summary>
public interface IDestinationSessionService
{
    Task<DestinationSession> OpenAsync(
        string selectedPath,
        SourceBindingIdentity source,
        OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken);
}

public sealed class DestinationSessionService : IDestinationSessionService
{
    private readonly IDestinationValidator _validator;
    private readonly IDestinationLayoutService _layout;
    private readonly IDestinationLockService _lockService;
    private readonly IDestinationBindingService _bindingService;
    private readonly ILogger<DestinationSessionService> _logger;

    public DestinationSessionService(
        IDestinationValidator validator,
        IDestinationLayoutService layout,
        IDestinationLockService lockService,
        IDestinationBindingService bindingService,
        ILogger<DestinationSessionService> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _bindingService = bindingService ?? throw new ArgumentNullException(nameof(bindingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DestinationSession> OpenAsync(
        string selectedPath,
        SourceBindingIdentity source,
        OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken)
    {
        var canonicalRoot = _validator.ValidateAndCanonicalize(selectedPath);
        var destination = _layout.EnsureLayout(canonicalRoot);
        var destinationLock = _lockService.Acquire(destination);

        try
        {
            var binding = await _bindingService
                .BindOrValidateAsync(destination, source, operatorIdentity, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Destination session opened; outcome={Outcome}; path={DestinationPath}",
                binding.Outcome, destination.RootPath);
            return new DestinationSession(destination, binding, destinationLock);
        }
        catch
        {
            await destinationLock.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
