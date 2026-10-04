using MediatR;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

internal class StartRegistrationHandler(IDraftStateRepository draftRepository) : IRequestHandler<StartRegistrationCommand, string>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;   
    
    public async Task<string> Handle(StartRegistrationCommand request, CancellationToken cancellationToken)
    {
        // 1. Çalışanların aralarında WhatsApp'tan vs. kolayca paylaşabileceği 
        // 6 haneli okunabilir bir anahtar üretiyoruz (Örn: "7F3B9A")
        string generatedKey = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

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
