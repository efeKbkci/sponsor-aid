using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Interfaces.Repositories;

public interface IRegistrationTrackingRepository
{
    Task CreateAsync(string registrationKey, CancellationToken cancellationToken = default);
    Task UpdateGuardianNameAsync(string registrationKey, string guardianName, CancellationToken cancellationToken = default);
    Task MarkAsCompletedAsync(string registrationKey, CancellationToken cancellationToken = default);
}