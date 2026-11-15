using SponsorAid.Domain.Enums;

namespace SponsorAid.Infrastructure.Persistence;

/// <summary>
/// Database entity that tracks the lifecycle status and metadata of a registration.
/// </summary>
public class RegistrationTrackingRecord
{
    // Primary key that links this record to the corresponding registration draft.
    public string RegistrationKey { get; set; } = string.Empty;

    public long ChatId { get; set; }

    public string? GuardianName { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.InProgress;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
