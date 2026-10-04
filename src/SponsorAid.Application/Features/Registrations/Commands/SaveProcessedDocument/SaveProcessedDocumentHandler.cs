using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument.Dtos;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models; // DTO'lar için
using System.Text.Json;
using SponsorAid.Domain.Exceptions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

public class SaveProcessedDocumentCommandHandler : IRequestHandler<SaveProcessedDocumentCommand>
{
    private readonly IDraftStateRepository _draftRepository;
    private readonly IUserNotifier _notifier; // Telegram/WhatsApp fark etmeksizin mesaj atan servis

    public SaveProcessedDocumentCommandHandler(
        IDraftStateRepository draftRepository,
        IUserNotifier notifier)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
    }

    public async Task Handle(SaveProcessedDocumentCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        // Kayıt bulunamazsa, bu durum bir hata olarak ele alınır.
        if (draft == null)
        {
            throw new NullReferenceException($"Draft with key {request.RegistrationKey} not found.");
        }

        // Worker seviyesindeki hatalar ele alınır (Örn: Fotoğraf çok bulanık)
        if (!request.IsSuccess)
        {
            await _notifier.SendMessageAsync(draft.ChatId, $"Belgeniz işlenirken bir sorun oluştu: {request.ErrorMessage}. Lütfen daha net bir belge yükleyiniz.");
            return;
        }

        // Domain Logic kontrol edilir
            if (request.DocumentType == "PopulationRegistry")
            {
                // Worker'dan gelen JSON verisini C# nesnesine çeviriyoruz. 
                // TODO: AutoMapper kullanacağız. 
                var data = JsonSerializer.Deserialize<PopulationRegistryDto>(request.ExtractedDataJson!);

                data = FilterChildren(data);
                ValidatePopulationRegistry(data, draft);

                draft.ApplyPopulationRegistryResult(data);

                draft.SystemDocuments.PopulationRegistryDocumentPath = request.SavedFilePath;
            }
            else if (request.DocumentType == "Residence")
            {
                var data = JsonSerializer.Deserialize<ResidenceDto>(request.ExtractedDataJson!);
                draft.ApplyResidenceDocumentResult(data);
                draft.SystemDocuments.ResidenceDocumentPath = request.SavedFilePath;
            }
            else if (request.DocumentType == "IdentityDocument")
            {
                draft.SystemDocuments.IdentityDocumentPath = request.SavedFilePath;
            }
            else
            {
                throw new NotSupportedException($"Belge türü {request.DocumentType} desteklenmiyor.");
            }

            // 3. HER ŞEY BAŞARILIYSA KAYDET
            await _draftRepository.SaveAsync(draft, cancellationToken);

            // 4. KULLANICIYA MÜJDEYİ VER
            await _notifier.SendMessageAsync(draft.ChatId, "✅ Belgeniz başarıyla onaylandı ve dosyanıza eklendi. İşleme devam edebilirsiniz.");
    }

    private void ValidatePopulationRegistry(PopulationRegistryDto data, RegistrationDraftState draft)
    {
        if (!string.Equals(draft.Guardian.Name, data.Mother.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentValidationException("Nüfus belgesindeki anne/eş ismi, girdiğiniz vasi ismi ile eşleşmiyor. E-Devlet'ten belge türü yanlış seçilmiş olabilir.");
        }
    }

    private PopulationRegistryDto FilterChildren(PopulationRegistryDto data)
    {
        var validChildren = data.Children
            .Where(c => (DateTime.Today.Year - c.BirthDate.Year) < 15)
            .ToList();
        
        return data with { Children = validChildren };
    }
}