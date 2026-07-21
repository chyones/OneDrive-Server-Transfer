namespace OneDriveServerTransfer.Destination;

/// <summary>
/// A user-facing destination error with a short title, plain-language explanation,
/// corrective action, and stable reference code. It never carries tenant IDs, drive
/// IDs, tokens, protected database values, or stack traces in its user-facing fields.
/// </summary>
public sealed class DestinationException : Exception
{
    public DestinationException(
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
