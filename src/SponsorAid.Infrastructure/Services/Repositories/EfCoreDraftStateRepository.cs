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

    public async Task<RegistrationDraftState?> GetActiveByChatIdAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var record = await _context.Drafts
            .FirstOrDefaultAsync(d => d.ChatId == chatId && d.IsActive, cancellationToken);

        if (record == null) return null;

        var state = JsonSerializer.Deserialize<RegistrationDraftState>(record.JsonData);
        if (state != null)
        {
            state.IsActive = record.IsActive;
        }
        return state;
    }

    public async Task SaveAsync(RegistrationDraftState state, CancellationToken cancellationToken = default)
    {
        var record = await _context.Drafts.FirstOrDefaultAsync(d => d.RegistrationKey == state.RegistrationKey, cancellationToken);
        state.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(state);

        // 🚀 Eğer bu taslak aktifse, kullanıcının o sohbetteki diğer tüm taslaklarını pasife al
        if (state.IsActive)
        {
            var otherActiveRecords = await _context.Drafts
                .Where(d => d.ChatId == state.ChatId && d.RegistrationKey != state.RegistrationKey && d.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in otherActiveRecords)
            {
                other.IsActive = false;
            }
        }

        if (record == null)
        {
            record = new DraftRecord
            {
                RegistrationKey = state.RegistrationKey,
                ChatId = state.ChatId,
                CreatedAt = state.CreatedAt,
                UpdatedAt = state.UpdatedAt,
                IsActive = state.IsActive,
                JsonData = json
            };
            _context.Drafts.Add(record);
        }
        else
        {
            record.ChatId = state.ChatId; // Sohbet transferi durumunda yeni ChatId atanır
            record.UpdatedAt = state.UpdatedAt;
            record.IsActive = state.IsActive;
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