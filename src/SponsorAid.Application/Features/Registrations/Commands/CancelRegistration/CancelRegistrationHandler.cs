using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;

namespace SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;

/// <summary>
/// Handles registration cancellation by removing draft data, tracking records, and uploaded files.
/// </summary>
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
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        if (draft != null)
        {
            DeleteUploadedFiles(draft);
            await _draftRepository.DeleteAsync(request.RegistrationKey, cancellationToken);
            await _trackingRepository.RemoveAsync(request.RegistrationKey, cancellationToken);
        }

        await _notifier.SendMessageAsync(request.ChatId, _localizer["Success_RegistrationCancelled"]);
    }

    private void DeleteUploadedFiles(RegistrationDraftState draft)
    {
        var filesToDelete = new HashSet<string?>();

        filesToDelete.Add(draft.SystemDocuments.PopulationRegistryDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.ResidenceDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityFrontPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityBackPath);
        filesToDelete.Add(draft.SystemDocuments.MergedIdentityPath);
        filesToDelete.Add(draft.SystemDocuments.BankAccountDocumentPath);

        if (draft.Guardian != null)
        {
            filesToDelete.Add(draft.Guardian.IdentityDocumentPath);
        }

        if (draft.Children != null)
        {
            foreach (var child in draft.Children)
            {
                filesToDelete.Add(child.IdentityDocumentPath);
            }
        }

        // Removes uploaded physical files to prevent orphaned files on the storage system.
        foreach (var filePath in filesToDelete)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                _fileStorage.DeleteFile(filePath);
            }
        }
    }
}
