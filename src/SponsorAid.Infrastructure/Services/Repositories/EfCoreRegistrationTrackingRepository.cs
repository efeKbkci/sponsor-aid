using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;
using SponsorAid.Infrastructure.Persistence;

namespace SponsorAid.Infrastructure.Services.Repositories;

public class EfCoreRegistrationTrackingRepository(SponsorAidDbContext context) : IRegistrationTrackingRepository
{
    private readonly SponsorAidDbContext _context = context;

    public async Task CreateAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        var record = new RegistrationTrackingRecord
        {
            RegistrationKey = registrationKey,
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
}
