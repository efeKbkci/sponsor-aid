using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;

public class ProcessBinaryInputHandler(
    IDraftStateRepository draftRepository, 
    IMediator mediator, IUserNotifier notifier,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<ProcessBinaryInputCommand>
{
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

        switch (activeDraft.CurrentStep)
        {
            case RegistrationStep.WaitingForPopulationRegistry:
                activeDraft.SetStep(RegistrationStep.ProcessingPopulationRegistry);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingPopulationRegistry"]);

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "PopulationRegistry"), cancellationToken);
                break;

            case RegistrationStep.WaitingForResidence:
                activeDraft.SetStep(RegistrationStep.ProcessingResidence);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingResidence"]);

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "Residence"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityFront:
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityFront);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingIdentityFront"]);

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "front"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityBack:
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityBack);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_ProcessingIdentityBack"]);

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "back"), cancellationToken);
                break;

            // Kilit Altındaki Asenkron Durumlar
            case RegistrationStep.ProcessingPopulationRegistry:
            case RegistrationStep.ProcessingResidence:
            case RegistrationStep.ProcessingIdentityFront:
            case RegistrationStep.ProcessingIdentityBack:
            case RegistrationStep.MergingDocuments:
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_PreviousDocumentStillProcessing"]);
                break;

            // Belge Beklenmeyen Durumlar (Anket, Onay vb.)
            default:
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_DocumentUploadNotExpected"]);
                break;
        }
    }
}
