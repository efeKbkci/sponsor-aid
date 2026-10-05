using System.Collections.Concurrent;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;

namespace SponsorAid.Infrastructure.Services.Repositories;

public class InMemoryDraftStateRepository : IDraftStateRepository
{
    // Verileri RAM'de güvenli bir şekilde tutacağımız thread-safe sözlük.
    // Key: RegistrationKey, Value: RegistrationDraftState
    private readonly ConcurrentDictionary<string, RegistrationDraftState> _store = new();

    public Task<RegistrationDraftState?> GetAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(registrationKey, out var state);

        // In-Memory işlemler senkron çalışır ancak arayüz (Interface) Task beklediği için Task.FromResult dönüyoruz.
        return Task.FromResult(state);
    }

    public Task SaveAsync(RegistrationDraftState state, CancellationToken cancellationToken = default)
    {
        // AddOrUpdate: Kayıt yoksa ekler, varsa üzerine yazar.
        _store.AddOrUpdate(state.RegistrationKey, state, (_, _) => state);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string registrationKey, CancellationToken cancellationToken = default)
    {
        // TryRemove: Veriyi bulur ve siler. Bulamazsa sessizce geçer.
        _store.TryRemove(registrationKey, out _);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<RegistrationDraftState>> ListAllAsync(string chatId, CancellationToken cancellationToken = default)
    {
        // Interface'de chatId string olarak tasarlandı (ileride Guid vb. olabileceği için)[cite: 2].
        // State modelinde ise ChatId long tipinde[cite: 2]. Burada geçici bir dönüştürme yapıyoruz.
        if (long.TryParse(chatId, out var parsedChatId))
        {
            var results = _store.Values.Where(x => x.ChatId == parsedChatId).ToList();
            return Task.FromResult<IEnumerable<RegistrationDraftState>>(results);
        }

        // Eğer dönüştürülemezse boş liste dön.
        return Task.FromResult<IEnumerable<RegistrationDraftState>>([]);
    }
}