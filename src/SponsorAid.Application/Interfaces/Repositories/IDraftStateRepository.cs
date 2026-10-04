using SponsorAid.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces.Repositories;

public interface IDraftStateRepository
{
    // Kullanıcının (ChatId) mevcut taslağını getirir. Yoksa null döner.
    Task<RegistrationDraftState?> GetAsync(string registrationKey, CancellationToken cancellationToken = default);

    // Taslağı kaydeder veya günceller.
    Task SaveAsync(RegistrationDraftState state, CancellationToken cancellationToken = default);

    // İşlem başarıyla bitip IHH'ya iletildiğinde veya iptal edildiğinde taslağı siler.
    Task DeleteAsync(string registrationKey, CancellationToken cancellationToken = default);

    // chatId, daha sonraki sürümlerde kullanıcının kimliği ile değişecek. 
    Task<IEnumerable<RegistrationDraftState>> ListAllAsync(string chatId, CancellationToken cancellationToken = default);
}