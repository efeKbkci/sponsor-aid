namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Defines a command linked to a specific chat conversation.
/// </summary>
public interface IChatCommand
{
    /// <summary>
    /// Gets the unique identifier of the chat conversation.
    /// </summary>
    long ChatId { get; }
}
