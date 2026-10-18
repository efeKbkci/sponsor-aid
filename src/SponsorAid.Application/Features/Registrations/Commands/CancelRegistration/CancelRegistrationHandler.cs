using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;

namespace SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;

public class CancelRegistrationHandler(
    IDraftStateRepository draftRepository,
    IRegistrationTrackingRepository trackingRepository,
    IFileStorageService fileStorage,
    IUserNotifier notifier,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<CancelRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IFileStorageService _fileStorage = fileStorage;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(CancelRegistrationCommand request, CancellationToken cancellationToken)
    {
        // 1. Taslağı veritabanından getir
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        if (draft != null)
        {
            // 2. Sistemde fiziksel olarak duran dosyaları tespit et ve sil
            DeleteUploadedFiles(draft);

            // 3. Veritabanındaki kaydı (JSON verisini) tamamen sil
            await _draftRepository.DeleteAsync(request.RegistrationKey, cancellationToken);

            // 4. Takip edilen kayıtlardan taslağı kaldır
            await _trackingRepository.RemoveAsync(request.RegistrationKey, cancellationToken);
        }

        // 4. Kullanıcıya başarı mesajını gönder
        await _notifier.SendMessageAsync(request.ChatId, _localizer["Success_RegistrationCancelled"]);
    }

    private void DeleteUploadedFiles(RegistrationDraftState draft)
    {
        var filesToDelete = new HashSet<string?>();

        // Resmi Belgeler
        filesToDelete.Add(draft.SystemDocuments.PopulationRegistryDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.ResidenceDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityFrontPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityBackPath);
        filesToDelete.Add(draft.SystemDocuments.MergedIdentityPath);

        // Vasi Kimliği
        if (draft.Guardian != null)
        {
            filesToDelete.Add(draft.Guardian.IdentityDocumentPath);
        }

        // Çocukların Kimlikleri
        if (draft.Children != null)
        {
            foreach (var child in draft.Children)
            {
                filesToDelete.Add(child.IdentityDocumentPath);
            }
        }

        // Toplanan yollardaki fiziksel dosyaları güvenli bir şekilde sil
        foreach (var filePath in filesToDelete)
        {
            _fileStorage.DeleteFile(filePath);
        }
    }
}