using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Interfaces.Repositories;

public interface IRegistrationTrackingRepository
{
    // ChatId eklendi
    Task CreateAsync(string registrationKey, long chatId, CancellationToken cancellationToken = default);
    Task UpdateGuardianNameAsync(string registrationKey, string guardianName, CancellationToken cancellationToken = default);
    Task MarkAsCompletedAsync(string registrationKey, CancellationToken cancellationToken = default);

    // Listeleme ve Durum Sorgulama metotları
    Task<IEnumerable<(string RegistrationKey, string? GuardianName, RegistrationStatus Status)>> ListByChatIdAsync(long chatId, CancellationToken cancellationToken = default);
    Task<RegistrationStatus?> GetStatusAsync(string registrationKey, CancellationToken cancellationToken = default);
}