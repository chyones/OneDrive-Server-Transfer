using Microsoft.Extensions.Options;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Options wrapper that runs the real SourceResolutionOptionsValidator on Value access,
/// mirroring the production options pipeline (Options.Create skips validation).
/// </summary>
internal sealed class ValidatingSourceResolutionOptions(SourceResolutionOptions options)
    : IOptions<SourceResolutionOptions>
{
    public SourceResolutionOptions Value
    {
        get
        {
            var result = new SourceResolutionOptionsValidator().Validate(null, options);
            if (result.Failed)
            {
                throw new OptionsValidationException(
                    nameof(SourceResolutionOptions),
                    typeof(SourceResolutionOptions),
                    result.Failures ?? []);
            }

            return options;
        }
    }
}
