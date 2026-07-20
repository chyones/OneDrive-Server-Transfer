namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// A user-facing source-resolution error with a short title, plain-language
/// explanation, corrective action, and stable reference code. It never carries
/// passwords, tokens, URLs with identifiers, raw Graph responses, or stack traces.
/// </summary>
public sealed class SourceResolutionException : Exception
{
    public SourceResolutionException(
        string referenceCode,
        string title,
        string explanation,
        string correctiveAction,
        Exception? innerException = null)
        : base($"{referenceCode}: {title}", innerException)
    {
        ReferenceCode = referenceCode;
        Title = title;
        Explanation = explanation;
        CorrectiveAction = correctiveAction;
    }

    public string ReferenceCode { get; }

    public string Title { get; }

    public string Explanation { get; }

    public string CorrectiveAction { get; }
}
