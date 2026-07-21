using System.IO;
using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Binds a destination to exactly one source (tenant ID, source drive ID, employee
/// Entra object ID; D-018) and validates that binding when an existing destination is
/// reopened. The signed-in operator is recorded as audit data only (D-032): any
/// operator who passed M2 tenant and allowlist authorization may resume a destination
/// whose tenant, employee, drive, and state checks all match. A destination bound to a
/// different source is rejected, and a non-empty destination without valid application
/// state is never silently adopted.
/// </summary>
public interface IDestinationBindingService
{
    Task<DestinationBindingResult> BindOrValidateAsync(
        ResolvedDestination destination,
        SourceBindingIdentity source,
        OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken);
}

public sealed class DestinationBindingService : IDestinationBindingService
{
    internal const string BoundAction = "Bound";
    internal const string ResumeValidatedAction = "ResumeValidated";

    private readonly IDestinationBindingStore _store;
    private readonly IDestinationLayoutService _layout;
    private readonly ILogger<DestinationBindingService> _logger;

    public DestinationBindingService(
        IDestinationBindingStore store,
        IDestinationLayoutService layout,
        ILogger<DestinationBindingService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DestinationBindingResult> BindOrValidateAsync(
        ResolvedDestination destination,
        SourceBindingIdentity source,
        OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operatorIdentity);

        var databasePath = destination.StateDatabasePath;
        var stateExists = File.Exists(databasePath);

        if (!stateExists && _layout.HasForeignContent(destination))
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=NonEmptyWithoutState; path={DestinationPath}",
                DestinationErrorCodes.NonEmptyDestinationWithoutState, destination.RootPath);
            throw DestinationErrors.NonEmptyDestinationWithoutState();
        }

        if (stateExists)
        {
            await _store.ValidateIntegrityAsync(databasePath, cancellationToken).ConfigureAwait(false);
        }

        var stored = await _store.GetBindingAsync(databasePath, cancellationToken).ConfigureAwait(false);

        if (stored is null)
        {
            if (_layout.HasForeignContent(destination))
            {
                _logger.LogWarning(
                    "Destination rejected; code={ReferenceCode}; reason=StateWithoutBinding; path={DestinationPath}",
                    DestinationErrorCodes.NonEmptyDestinationWithoutState, destination.RootPath);
                throw DestinationErrors.NonEmptyDestinationWithoutState();
            }

            await PersistNewBindingAsync(databasePath, source, operatorIdentity, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Destination bound to a new source; outcome={Outcome}; path={DestinationPath}",
                DestinationBindingOutcome.BoundNew, destination.RootPath);
            return new DestinationBindingResult(DestinationBindingOutcome.BoundNew);
        }

        if (!BindingMatches(stored, source))
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=ForeignSourceBinding; path={DestinationPath}",
                DestinationErrorCodes.ForeignSourceBinding, destination.RootPath);
            throw DestinationErrors.ForeignSourceBinding();
        }

        await _store.RecordOperatorAuditAsync(
            databasePath, operatorIdentity, ResumeValidatedAction, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Destination binding validated for resume; outcome={Outcome}; path={DestinationPath}",
            DestinationBindingOutcome.ResumedExisting, destination.RootPath);
        return new DestinationBindingResult(DestinationBindingOutcome.ResumedExisting);
    }

    private async Task PersistNewBindingAsync(
        string databasePath,
        SourceBindingIdentity source,
        OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.CreateBindingAsync(
                databasePath,
                new StoredDestinationBinding(
                    source.TenantId,
                    source.DriveId,
                    source.EmployeeObjectId,
                    source.EmployeeUpn,
                    operatorIdentity.ObjectId,
                    operatorIdentity.UserPrincipalName,
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            await _store.RecordOperatorAuditAsync(
                databasePath, operatorIdentity, BoundAction, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger.LogWarning(
                "Destination binding could not be persisted; code={ReferenceCode}; path={DatabasePath}",
                DestinationErrorCodes.DestinationStateFailure, databasePath);
            throw DestinationErrors.DestinationStateFailure(exception);
        }
    }

    private static bool BindingMatches(StoredDestinationBinding stored, SourceBindingIdentity source) =>
        string.Equals(stored.TenantId, source.TenantId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(stored.EmployeeObjectId, source.EmployeeObjectId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(stored.DriveId, source.DriveId, StringComparison.Ordinal);
}
