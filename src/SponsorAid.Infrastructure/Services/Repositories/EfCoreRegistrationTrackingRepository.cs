using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;
using SponsorAid.Infrastructure.Persistence;

namespace SponsorAid.Infrastructure.Services.Repositories;

public class EfCoreRegistrationTrackingRepository(SponsorAidDbContext context) : IRegistrationTrackingRepository
{
    private readonly SponsorAidDbContext _context = context;

    public async Task CreateAsync(string registrationKey, long chatId, CancellationToken cancellationToken = default)
    {
        var record = new RegistrationTrackingRecord
        {
            RegistrationKey = registrationKey,
            ChatId = chatId, // ChatId eklendi
            Status = RegistrationStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RegistrationTrackings.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGuardianNameAsync(string registrationKey, string guardianName, CancellationToken cancellationToken = default)
    {
        var record = await _context.RegistrationTrackings.FirstOrDefaultAsync(r => r.RegistrationKey == registrationKey, cancellationToken);
        if (record != null)
        {
            record.GuardianName = guardianName;
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsCompletedAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        var record = await _context.RegistrationTrackings.FirstOrDefaultAsync(r => r.RegistrationKey == registrationKey, cancellationToken);
        if (record != null)
        {
            record.Status = RegistrationStatus.Completed;
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // 🚀 YENİ: ChatId'ye göre listeleme
    public async Task<IEnumerable<(string RegistrationKey, string? GuardianName, RegistrationStatus Status)>> ListByChatIdAsync(long chatId, CancellationToken cancellationToken = default)
    {
        return await _context.RegistrationTrackings
            .AsNoTracking()
            .Where(r => r.ChatId == chatId)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ValueTuple<string, string?, RegistrationStatus>(r.RegistrationKey, r.GuardianName, r.Status))
            .ToListAsync(cancellationToken);
    }

    // 🚀 YENİ: Sadece durumu getirme
    public async Task<RegistrationStatus?> GetStatusAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        var record = await _context.RegistrationTrackings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RegistrationKey == registrationKey, cancellationToken);

        return record?.Status;
    }
}