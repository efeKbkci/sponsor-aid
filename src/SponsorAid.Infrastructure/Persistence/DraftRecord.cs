namespace SponsorAid.Infrastructure.Persistence;

/// <summary>
/// Database entity that stores temporary registration draft states.
/// </summary>
public class DraftRecord
{
    // Primary key that uniquely identifies each registration session.
    public string RegistrationKey { get; set; } = string.Empty;

    public long ChatId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Indicates whether this draft is the active conversation session for the chat.
    public bool IsActive { get; set; } = true;

    // Stores the complete registration draft model as serialized JSON data in PostgreSQL.
    public string JsonData { get; set; } = string.Empty;
}
