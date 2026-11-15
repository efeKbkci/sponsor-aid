using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Infrastructure.Persistence;

namespace SponsorAid.Infrastructure.Services.Repositories;

/// <summary>
/// Manages draft state persistence and lifecycle using Entity Framework Core.
/// </summary>
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

        // Deserializes the database JSON string into the draft state domain model.
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

        // Deactivates other drafts for this chat so only one draft remains active at a time.
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
            // Updates chat ID to support scenarios where a registration continues in a new chat.
            record.ChatId = state.ChatId;
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
