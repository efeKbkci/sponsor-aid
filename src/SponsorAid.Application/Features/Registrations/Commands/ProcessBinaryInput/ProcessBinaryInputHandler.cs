using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBankAccountDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;

/// <summary>
/// Validates and routes uploaded binary files to the appropriate document processor based on the active registration step.
/// </summary>
public class ProcessBinaryInputHandler(
    IDraftStateRepository draftRepository, 
    IMediator mediator, IUserNotifier notifier,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<ProcessBinaryInputCommand>
{
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;

    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(ProcessBinaryInputCommand request, CancellationToken cancellationToken)
    {
        var activeDraft = await _draftRepository.GetActiveByChatIdAsync(request.ChatId, cancellationToken);

        if (activeDraft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_NoActiveRegistration_StartWithCommand"]);
            return;
        }

        var normalizedExtension = (request.Extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

        switch (activeDraft.CurrentStep)
        {
            case RegistrationStep.WaitingForPopulationRegistry:
                ValidateDocumentFile(request.FileSize, normalizedExtension);
                activeDraft.SetStep(RegistrationStep.ProcessingPopulationRegistry);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingPopulationRegistry"]);

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, normalizedExtension, "PopulationRegistry"), cancellationToken);
                break;

            case RegistrationStep.WaitingForResidence:
                ValidateDocumentFile(request.FileSize, normalizedExtension);
                activeDraft.SetStep(RegistrationStep.ProcessingResidence);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingResidence"]);

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, normalizedExtension, "Residence"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityFront:
                ValidateIdentityImageFile(request.FileSize, normalizedExtension);
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityFront);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingIdentityFront"]);

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, normalizedExtension, "front"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityBack:
                ValidateIdentityImageFile(request.FileSize, normalizedExtension);
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityBack);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingIdentityBack"]);

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, normalizedExtension, "back"), cancellationToken);
                break;

            case RegistrationStep.WaitingForBankAccountDocument:
                ValidateDocumentFile(request.FileSize, normalizedExtension);
                activeDraft.SetStep(RegistrationStep.ProcessingBankAccountDocument);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingBankAccountDocument"]);

                await _mediator.Send(new ProcessBankAccountDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, normalizedExtension), cancellationToken);
                break;

            // Prevents duplicate processing requests while an asynchronous job is currently running.
            case RegistrationStep.ProcessingPopulationRegistry:
            case RegistrationStep.ProcessingResidence:
            case RegistrationStep.ProcessingIdentityFront:
            case RegistrationStep.ProcessingIdentityBack:
            case RegistrationStep.MergingDocuments:
            case RegistrationStep.ProcessingBankAccountDocument:
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_PreviousDocumentStillProcessing"]);
                break;

            // Rejects file uploads when the registration workflow is currently expecting text answers or confirmation.
            default:
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_DocumentUploadNotExpected"]);
                break;
        }
    }

    private static void ValidateDocumentFile(long fileSize, string extension)
    {
        if (fileSize > MaxFileSizeInBytes)
        {
            throw new BusinessRuleException("Error_DocumentFileTooLarge");
        }

        if (extension is not ("pdf" or "jpg" or "jpeg" or "png"))
        {
            throw new BusinessRuleException("Error_UnsupportedDocumentFileType");
        }
    }

    private static void ValidateIdentityImageFile(long fileSize, string extension)
    {
        if (fileSize > MaxFileSizeInBytes)
        {
            throw new BusinessRuleException("Error_IdentityImageTooLarge");
        }

        if (extension is not ("jpg" or "jpeg" or "png"))
        {
            throw new BusinessRuleException("Error_UnsupportedIdentityImageType");
        }
    }
}
