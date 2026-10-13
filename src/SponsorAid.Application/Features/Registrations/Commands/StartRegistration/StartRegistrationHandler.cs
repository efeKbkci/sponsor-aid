using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

public class StartRegistrationHandler(
    IDraftStateRepository draftRepository, 
    IRegistrationTrackingRepository trackingRepository,
    IUserNotifier notifier
    ) : IRequestHandler<StartRegistrationCommand, string>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IUserNotifier _notifier = notifier;

    public async Task<string> Handle(StartRegistrationCommand request, CancellationToken cancellationToken)
    {
        // 1. Çalışanların aralarında WhatsApp'tan vs. kolayca paylaşabileceği 
        // 6 haneli okunabilir bir anahtar üretiyoruz (Örn: "7F3B9A")
        // GUID 16 karakter içerirken chars dizisi 36 karakter içerir. Collision olasılığı çok daha düşecektir. 
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string generatedKey;

        // PNR Kuralı: Anahtarın sadece rakamlardan oluşmasını engelle (Para miktarlarıyla asla çakışmasın)
        do
        {
            generatedKey = RandomNumberGenerator.GetString(chars, 6);
        }
        while (generatedKey.All(char.IsDigit));

        // 2. Sepeti yeni anahtarla oluşturuyoruz
        var newDraft = new RegistrationDraftState
        {
            RegistrationKey = generatedKey,
            ChatId = request.ChatId,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Kaydediyoruz
        await _draftRepository.SaveAsync(newDraft, cancellationToken);
        // 2. Takip tablosuna 'Devam Ediyor' olarak ekle
        await _trackingRepository.CreateAsync(generatedKey, cancellationToken);

        // 4. Anahtarı dış dünyaya (Bot'a) döndürüyoruz
        await _notifier.SendMessageAsync(request.ChatId, "✅ Yeni bir kayıt başlatıldı!\n\nLütfen vasiye ait T.C. Kimlik numarasını metin olarak yazınız.");
        return generatedKey;
    }
}
