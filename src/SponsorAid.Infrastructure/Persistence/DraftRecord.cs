namespace SponsorAid.Infrastructure.Persistence;

public class DraftRecord
{
    public string RegistrationKey { get; set; } = string.Empty; // Primary Key
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // RegistrationDraftState nesnesini bu sütunda JSON olarak tutacağız
    public string JsonData { get; set; } = string.Empty;
}