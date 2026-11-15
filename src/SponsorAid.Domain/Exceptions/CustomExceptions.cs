namespace SponsorAid.Domain.Exceptions;

/// <summary>
/// Base exception for domain errors containing localization arguments.
/// </summary>
public abstract class SponsorAidException : Exception
{
    /// <summary>
    /// Initializes domain exception with a message key and format arguments.
    /// </summary>
    /// <param name="message">Translation key or error message.</param>
    /// <param name="arguments">Positional values for message placeholders.</param>
    protected SponsorAidException(string message, params object[] arguments) : base(message)
    {
        Arguments = arguments;
    }

    public IReadOnlyList<object> Arguments { get; }
}

/// <summary>
/// Thrown when an operation violates core domain rules or validation constraints.
/// </summary>
public class BusinessRuleException : SponsorAidException
{
    /// <summary>
    /// Initializes business rule violation with a rule message key and optional arguments.
    /// </summary>
    /// <param name="message">Translation key for the rule violation.</param>
    /// <param name="arguments">Positional values for message placeholders.</param>
    public BusinessRuleException(string message, params object[] arguments) : base(message, arguments) { }
}

/// <summary>
/// Thrown when submitted documents or extracted data fail verification checks.
/// </summary>
public class DocumentValidationException : SponsorAidException
{
    /// <summary>
    /// Initializes document validation failure with a message key and optional arguments.
    /// </summary>
    /// <param name="message">Translation key for the validation failure.</param>
    /// <param name="arguments">Positional values for message placeholders.</param>
    public DocumentValidationException(string message, params object[] arguments) : base(message, arguments) { }
}
