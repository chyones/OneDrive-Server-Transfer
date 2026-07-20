namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// A user-facing authentication error containing a short title, a plain-language
/// explanation, a corrective action, and a stable reference code. It never carries
/// passwords, tokens, temporary URLs, raw identity-provider responses, authorization
/// headers, tenant IDs, account object IDs, or stack traces.
/// </summary>
public sealed class UserFacingAuthException : Exception
{
    public UserFacingAuthException(
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
