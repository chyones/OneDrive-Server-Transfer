using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Resolves one employee's business OneDrive root from a UPN or a OneDrive root URL
/// using only the approved v1.0 endpoints GRAPH-SRC-001/002/003. The dry run, file
/// enumeration, transfer, destination, and report behavior are out of scope here.
/// </summary>
public sealed class EmployeeSourceResolver : IEmployeeSourceResolver
{
    private const string BusinessDriveType = "business";

    private readonly IGraphRequestChannel _channel;
    private readonly IOptions<SourceResolutionOptions> _sourceOptions;
    private readonly IOptions<AuthenticationOptions> _authenticationOptions;
    private readonly ILogger<EmployeeSourceResolver> _logger;

    public EmployeeSourceResolver(
        IGraphRequestChannel channel,
        IOptions<SourceResolutionOptions> sourceOptions,
        IOptions<AuthenticationOptions> authenticationOptions,
        ILogger<EmployeeSourceResolver> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _sourceOptions = sourceOptions ?? throw new ArgumentNullException(nameof(sourceOptions));
        _authenticationOptions = authenticationOptions ?? throw new ArgumentNullException(nameof(authenticationOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResolvedEmployeeSource> ResolveAsync(string input, CancellationToken cancellationToken)
    {
        if (!EmployeeSourceInputParser.TryParse(input, out var parsed, out var parseError) || parsed is null)
        {
            throw parseError ?? SourceResolutionErrors.InvalidEmployeeInput();
        }

        var options = GetValidatedOptions();

        try
        {
            return parsed.Kind == EmployeeSourceInputKind.Upn
                ? await ResolveByUpnAsync(parsed.Upn!, options, cancellationToken).ConfigureAwait(false)
                : await ResolveByUrlAsync(parsed.Url!, options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Source resolution cancelled; mode={Mode}", parsed.Kind);
            throw SourceResolutionErrors.Cancelled();
        }
        catch (GraphRequestException exception)
        {
            throw MapGraphFailure(exception);
        }
    }

    private async Task<ResolvedEmployeeSource> ResolveByUpnAsync(
        string upn,
        SourceResolutionOptions options,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(string.Format(GraphEndpoints.UserDriveTemplate, Uri.EscapeDataString(upn)));
        using var document = await _channel.GetJsonAsync(uri, "/users/{upn}/drive", cancellationToken)
            .ConfigureAwait(false);

        var drive = GraphResponseParser.ParseDrive(document);
        ValidateBusinessDrive(drive);

        _logger.LogInformation(
            "Employee source resolved; mode={Mode}; tenantMatch={TenantMatch}; driveType={DriveType}",
            EmployeeSourceMode.Upn, true, drive.DriveType);

        return new ResolvedEmployeeSource(
            TenantId: _authenticationOptions.Value.TenantId,
            UserObjectId: drive.OwnerUserId!,
            UserPrincipalName: upn,
            DisplayName: drive.OwnerDisplayName ?? upn,
            DriveId: drive.Id!,
            DriveType: BusinessDriveType,
            DriveOwnerDisplayName: drive.OwnerDisplayName,
            DriveWebUrl: drive.WebUrl!,
            drive.QuotaTotalBytes,
            drive.QuotaUsedBytes,
            drive.QuotaRemainingBytes,
            EmployeeSourceMode.Upn,
            IsTenantConfirmed: true);
    }

    private async Task<ResolvedEmployeeSource> ResolveByUrlAsync(
        ParsedOneDriveUrl url,
        SourceResolutionOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(url.Host, options.TenantOneDriveHost, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Source URL host does not match the configured tenant OneDrive host.");
            throw SourceResolutionErrors.SourceOutsideTenant();
        }

        var siteUri = new Uri(string.Format(GraphEndpoints.SiteByPathTemplate, url.Host, url.SitePath));
        using var siteDocument = await _channel.GetJsonAsync(siteUri, "/sites/{hostname}:/{path}", cancellationToken)
            .ConfigureAwait(false);

        var site = GraphResponseParser.ParseSite(siteDocument);
        if (site.IsPersonalSite != true ||
            string.IsNullOrEmpty(site.Id) ||
            !string.Equals(site.SiteCollectionHostName, options.TenantOneDriveHost, StringComparison.OrdinalIgnoreCase))
        {
            throw SourceResolutionErrors.UnsupportedSource();
        }

        var driveUri = new Uri(string.Format(GraphEndpoints.SiteDriveTemplate, site.Id));
        using var driveDocument = await _channel.GetJsonAsync(driveUri, "/sites/{site-id}/drive", cancellationToken)
            .ConfigureAwait(false);

        var drive = GraphResponseParser.ParseDrive(driveDocument);
        ValidateBusinessDrive(drive);

        _logger.LogInformation(
            "Employee source resolved; mode={Mode}; tenantMatch={TenantMatch}; driveType={DriveType}",
            EmployeeSourceMode.OneDriveRootUrl, true, drive.DriveType);

        return new ResolvedEmployeeSource(
            TenantId: _authenticationOptions.Value.TenantId,
            UserObjectId: drive.OwnerUserId!,
            UserPrincipalName: null,
            DisplayName: drive.OwnerDisplayName ?? "Unknown employee",
            DriveId: drive.Id!,
            DriveType: BusinessDriveType,
            DriveOwnerDisplayName: drive.OwnerDisplayName,
            DriveWebUrl: drive.WebUrl!,
            drive.QuotaTotalBytes,
            drive.QuotaUsedBytes,
            drive.QuotaRemainingBytes,
            EmployeeSourceMode.OneDriveRootUrl,
            IsTenantConfirmed: true);
    }

    private static void ValidateBusinessDrive(GraphDriveData drive)
    {
        if (string.IsNullOrEmpty(drive.Id) || string.IsNullOrEmpty(drive.WebUrl))
        {
            throw SourceResolutionErrors.UnexpectedResponse();
        }

        if (!string.Equals(drive.DriveType, BusinessDriveType, StringComparison.OrdinalIgnoreCase))
        {
            throw SourceResolutionErrors.UnsupportedSource();
        }

        if (string.IsNullOrEmpty(drive.OwnerUserId))
        {
            throw SourceResolutionErrors.UnexpectedResponse();
        }
    }

    private SourceResolutionOptions GetValidatedOptions()
    {
        try
        {
            return _sourceOptions.Value;
        }
        catch (OptionsValidationException exception)
        {
            _logger.LogError("Source-resolution configuration is invalid.");
            throw SourceResolutionErrors.InvalidConfiguration(exception);
        }
    }

    private Exception MapGraphFailure(GraphRequestException exception)
    {
        string referenceCode;
        Exception failure;

        if (exception.StatusCode == 404)
        {
            failure = IsNotProvisionedHint(exception)
                ? SourceResolutionErrors.OneDriveNotProvisioned()
                : SourceResolutionErrors.EmployeeNotFound();
            referenceCode = ((SourceResolutionException)failure).ReferenceCode;
        }
        else if (exception.StatusCode == 403)
        {
            failure = SourceResolutionErrors.OperatorLacksAccess();
            referenceCode = SourceResolutionErrorCodes.OperatorLacksAccess;
        }
        else if (exception.StatusCode == 401)
        {
            failure = AuthenticationErrors.SessionUnauthorized();
            referenceCode = AuthenticationErrorCodes.SessionUnauthorized;
        }
        else if (exception.StatusCode == 429)
        {
            failure = SourceResolutionErrors.Throttled();
            referenceCode = SourceResolutionErrorCodes.Throttled;
        }
        else if (exception.IsTransient)
        {
            failure = SourceResolutionErrors.ServiceUnavailable();
            referenceCode = SourceResolutionErrorCodes.ServiceUnavailable;
        }
        else
        {
            failure = SourceResolutionErrors.UnexpectedResponse();
            referenceCode = SourceResolutionErrorCodes.UnexpectedResponse;
        }

        _logger.LogWarning(
            "Source resolution failed; reference={Reference}; status={Status}; code={Code}",
            referenceCode,
            exception.StatusCode?.ToString() ?? "n/a",
            exception.GraphErrorCode ?? "n/a");

        return failure;
    }

    private static bool IsNotProvisionedHint(GraphRequestException exception) =>
        exception.ErrorHintForClassification?.Contains("provision", StringComparison.OrdinalIgnoreCase) == true;
}
