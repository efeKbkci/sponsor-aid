using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using System.Text.RegularExpressions;

namespace SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;

/// <summary>
/// Handles resuming an incomplete registration or prompting the user to select one.
/// </summary>
public class ContinueRegistrationHandler(
    IDraftStateRepository draftRepository,
    IRegistrationTrackingRepository trackingRepository,
    IUserNotifier notifier,
    IMediator mediator,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<ContinueRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IMediator _mediator = mediator;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(ContinueRegistrationCommand request, CancellationToken cancellationToken)
    {
        // Displays interactive selection buttons when the user initiates continuation without specifying a key.
        if (string.IsNullOrWhiteSpace(request.RegistrationKey))
        {
            var trackings = await _trackingRepository.ListByChatIdAsync(request.ChatId, cancellationToken);
            var options = new Dictionary<string, string>();

            foreach (var track in trackings)
            {
                var guardianName = string.IsNullOrWhiteSpace(track.GuardianName)
                    ? _localizer["Label_AnonymousGuardian"].Value
                    : track.GuardianName;

                var statusLabel = _localizer[$"Enum_RegistrationStatus_{track.Status}"].Value;

                options[ContinueRegistrationCommand.SelectionCallbackPrefix + track.RegistrationKey] = $"{guardianName} - {statusLabel}";
            }

            if (options.Count == 0)
            {
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_NoRegistrationToContinue"]);
                return;
            }

            await _notifier.SendQuestionWithButtonsAsync(request.ChatId, _localizer["Prompt_SelectRegistration"], options);
            return;
        }

        var registrationKey = request.RegistrationKey.Trim().ToUpperInvariant();
        var draft = await _draftRepository.GetAsync(registrationKey, cancellationToken);

        if (draft == null)
        {
            // Checks tracking history to inform the user if the registration was already submitted.
            var trackingStatus = await _trackingRepository.GetStatusAsync(registrationKey, cancellationToken);
            if (trackingStatus == RegistrationStatus.Completed)
            {
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_RegistrationAlreadyCompleted", registrationKey].Value);
                return;
            }

            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_RegistrationNotFound", registrationKey].Value);
            return;
        }

        if (draft.CurrentStep == RegistrationStep.Completed)
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Info_RegistrationAlreadyCompleted", registrationKey].Value);
            return;
        }

        draft.ChatId = request.ChatId;
        draft.IsActive = true;
        draft.RevertProcessingState();
        await _draftRepository.SaveAsync(draft, cancellationToken);

        await SendResumePromptAsync(draft);
    }

    private async Task SendResumePromptAsync(RegistrationDraftState draft)
    {
        switch (draft.CurrentStep)
        {
            case RegistrationStep.WaitingForGuardianId:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeGuardianId", draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForPopulationRegistry:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumePopulationRegistry", draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForResidence:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeResidence", draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForIdentityFront:
                string targetFront = draft.CurrentDocumentTargetName == "Vasi" ? _localizer["Label_Guardian"].Value : _localizer["Label_ChildNamed", draft.CurrentDocumentTargetName].Value;
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeIdentityFront", draft.RegistrationKey, targetFront].Value);
                break;

            case RegistrationStep.WaitingForIdentityBack:
                string targetBack = draft.CurrentDocumentTargetName == "Vasi" ? _localizer["Label_Guardian"].Value : _localizer["Label_ChildNamed", draft.CurrentDocumentTargetName].Value;
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeIdentityBack", draft.RegistrationKey, targetBack].Value);
                break;

            case RegistrationStep.WaitingForBankAccountDocument:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeBankAccountDocument", draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForIban:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeIban", draft.RegistrationKey].Value);
                break;

            case RegistrationStep.WaitingForQuestions:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_ResumeQuestionnaire", draft.RegistrationKey].Value);
                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey));
                break;

            case RegistrationStep.WaitingForConfirmation:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_ResumeConfirmation", draft.RegistrationKey].Value);
                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey));
                break;

            default:
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_ResumeSendMessage", draft.RegistrationKey].Value);
                break;
        }
    }
}
