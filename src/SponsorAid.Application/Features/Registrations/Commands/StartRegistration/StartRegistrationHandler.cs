using MediatR;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

public class StartRegistrationHandler(IDraftStateRepository draftRepository) : IRequestHandler<StartRegistrationCommand, string>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;   
    
    public async Task<string> Handle(StartRegistrationCommand request, CancellationToken cancellationToken)
    {
        // 1. Çalışanların aralarında WhatsApp'tan vs. kolayca paylaşabileceği 
        // 6 haneli okunabilir bir anahtar üretiyoruz (Örn: "7F3B9A")
        // GUID 16 karakter içerirken chars dizisi 36 karakter içerir. Collision olasılığı çok daha düşecektir. 
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string generatedKey = RandomNumberGenerator.GetString(chars, 6);

        // 2. Sepeti yeni anahtarla oluşturuyoruz
        var newDraft = new RegistrationDraftState
        {
            RegistrationKey = generatedKey,
            ChatId = request.ChatId,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Kaydediyoruz
        await _draftRepository.SaveAsync(newDraft, cancellationToken);

        // 4. Anahtarı dış dünyaya (Bot'a) döndürüyoruz
        return generatedKey;
    }
}
