using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Infrastructure.Persistence;

namespace SponsorAid.Infrastructure.Services.Repositories;

public class EfCoreDraftStateRepository : IDraftStateRepository
{
    private readonly SponsorAidDbContext _context;

    public EfCoreDraftStateRepository(SponsorAidDbContext context)
    {
        _context = context;
    }

    public async Task<RegistrationDraftState?> GetAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        var record = await _context.Drafts.FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey, cancellationToken);

        if (record == null) return null;

        // Veritabanındaki JSON'ı al ve bizim zengin C# nesnemize çevir
        return JsonSerializer.Deserialize<RegistrationDraftState>(record.JsonData);
    }

    public async Task SaveAsync(RegistrationDraftState state, CancellationToken cancellationToken = default)
    {
        var record = await _context.Drafts.FirstOrDefaultAsync(d => d.RegistrationKey == state.RegistrationKey, cancellationToken);
        var json = JsonSerializer.Serialize(state);

        if (record == null)
        {
            record = new DraftRecord
            {
                RegistrationKey = state.RegistrationKey,
                ChatId = state.ChatId,
                CreatedAt = state.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                JsonData = json
            };
            _context.Drafts.Add(record);
        }
        else
        {
            record.UpdatedAt = DateTime.UtcNow;
            record.JsonData = json;
            _context.Drafts.Update(record);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        var record = await _context.Drafts.FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey, cancellationToken);
        if (record != null)
        {
            _context.Drafts.Remove(record);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<RegistrationDraftState>> ListAllAsync(string chatId, CancellationToken cancellationToken = default)
    {
        if (long.TryParse(chatId, out var parsedChatId))
        {
            var records = await _context.Drafts
                .Where(d => d.ChatId == parsedChatId)
                .ToListAsync(cancellationToken);

            return records
                .Select(r => JsonSerializer.Deserialize<RegistrationDraftState>(r.JsonData))
                .Where(state => state != null)!;
        }

        return [];
    }
}