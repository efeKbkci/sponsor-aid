using SponsorAid.Domain.Enums;

namespace SponsorAid.Infrastructure.Persistence;

public class RegistrationTrackingRecord
{
    public string RegistrationKey { get; set; } = string.Empty; // Primary Key
    public long ChatId { get; set; }
    public string? GuardianName { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.InProgress;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}