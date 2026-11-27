using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Integration.Contracts;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

/// <summary>
/// Applies parsed document data, manages state rollbacks on extraction failures, and advances identity verification.
/// </summary>
public class SaveProcessedDocumentCommandHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IEventPublisher eventPublisher,
    IEGovermentDocumentService eGovernment,
    IRegistrationTrackingRepository trackingRepository,
    IStringLocalizer<BotMessages> localizer,
    IMediator mediator
    ) : IRequestHandler<SaveProcessedDocumentCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly IEGovermentDocumentService _eGoverment = eGovernment;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(SaveProcessedDocumentCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null) throw new NullReferenceException($"Draft with key {request.RegistrationKey} not found.");

        // Reverts processing state back to waiting state so the user can re-upload after an extraction failure.
        if (!request.IsSuccess)
        {
            draft.RevertProcessingState();

            await _draftRepository.SaveAsync(draft, cancellationToken);
            await _notifier.SendMessageAsync(draft.ChatId, _localizer["Error_DocumentProcessingFailed", request.ErrorMessage ?? string.Empty].Value);
            return;
        }

        // Fetches verified PDF directly from e-Government portal when an image barcode is detected.
        if (!string.IsNullOrEmpty(request.ExtractedBarcode))
        {
            await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_EGovernmentBarcodeDetected", request.ExtractedBarcode].Value);

            try
            {
                string downloadedPdfPath = await _eGoverment.DownloadDocumentAsync(
                    request.ExtractedBarcode, draft.Guardian.IdNumber!.Value, cancellationToken
                );

                var task = new ProcessOfficialDocTask
                {
                    RegistrationKey = draft.RegistrationKey,
                    FilePath = downloadedPdfPath,
                    DocumentType = request.DocumentType == "PopulationRegistry"
                                    ? DocumentType.PopulationRegistry
                                    : DocumentType.Residence
                };

                var envelope = new WorkerTaskEnvelope
                {
                    CommandType = CommandType.ProcessOfficialDoc,
                    OfficialDocTask = task
                };

                // Re-enqueues official document task with downloaded PDF without changing draft state.
                await _eventPublisher.PublishAsync("document-processing-queue", envelope);
                return;
            }
            catch (Exception ex)
            {
                // Resets draft state to allow manual re-upload when external e-Government download fails.
                draft.RevertProcessingState();
                await _draftRepository.SaveAsync(draft, cancellationToken);
                throw new BusinessRuleException("Error_EGovernmentDocumentDownloadFailed");
            }
        }

        if (request.DocumentType == "PopulationRegistry")
        {
            var data = request.PopulationData!;
            draft.ApplyPopulationRegistryResult(data);
            draft.SystemDocuments.PopulationRegistryDocumentPath = request.SavedFilePath;
           
            if (!string.IsNullOrEmpty(draft.Guardian.Name))
            {
                await _trackingRepository.UpdateGuardianNameAsync(draft.RegistrationKey, draft.Guardian.Name, cancellationToken);
            }

            draft.SetStep(RegistrationStep.WaitingForResidence);

            string fatherDeathStr = draft.Father.DeathDate.HasValue
                ? draft.Father.DeathDate.Value.ToString("dd.MM.yyyy")
                : _localizer["Summary_NotSpecified"].Value;
            var eligibleChildren = draft.Children.Select(c => c.Name).ToList();
            string childrenStr = eligibleChildren.Any()
                ? string.Join(", ", eligibleChildren)
                : _localizer["Summary_NoEligibleChild"].Value;
            var populationSummary = new StringBuilder();
            populationSummary.AppendLine(_localizer["Summary_Population_Title"].Value);
            populationSummary.AppendLine(_localizer["Summary_Population_Mother", draft.Guardian.Name ?? _localizer["Summary_NotSpecified"].Value].Value);
            populationSummary.AppendLine(_localizer["Summary_Population_Father", draft.Father.Name ?? _localizer["Summary_NotSpecified"].Value].Value);
            populationSummary.AppendLine(_localizer["Summary_Population_FatherDeath", fatherDeathStr].Value);
            populationSummary.AppendLine(_localizer["Summary_Population_EligibleChildren", childrenStr].Value);
            await _notifier.SendMessageAsync(draft.ChatId, populationSummary.ToString());
            
            await _notifier.SendMessageAsync(draft.ChatId, _localizer["Success_PopulationRegistryProcessed"]);
        }
        else if (request.DocumentType == "Residence")
        {
            var data = request.ResidenceData!;
            draft.ApplyResidenceDocumentResult(data);
            draft.SystemDocuments.ResidenceDocumentPath = request.SavedFilePath;

            var targetName = draft.GetNextPersonNeedingIdentity();
            draft.SetDocumentTarget(targetName);
            draft.SetStep(RegistrationStep.WaitingForIdentityFront);

            var residenceSummary = new StringBuilder();
            residenceSummary.AppendLine(_localizer["Summary_Residence_Title"].Value);
            string fullAddress = !string.IsNullOrWhiteSpace(draft.ResidenceAddress?.FullAddress)
                ? draft.ResidenceAddress.FullAddress
                : _localizer["Summary_NotSpecified"].Value;
            residenceSummary.AppendLine(_localizer["Summary_Residence_Address", fullAddress].Value);
            await _notifier.SendMessageAsync(draft.ChatId, residenceSummary.ToString());

            await _notifier.SendMessageAsync(draft.ChatId, _localizer["Success_ResidenceProcessed"]);
        }
        else if (request.DocumentType == "IdentityFront" || request.DocumentType == "IdentityBack")
        {
            var extractedTc = request.ExtractedTcNumber!;
            var expectedTc = draft.GetExpectedIdNumberForCurrentTarget();

            // Validates that extracted identity number strictly matches the individual identified in population registry.
            if (expectedTc == null || extractedTc != expectedTc)
            {
                throw new DocumentValidationException("Error_IdentityNumberMismatch", extractedTc, draft.CurrentDocumentTargetName ?? string.Empty);
            }

            if (request.DocumentType == "IdentityFront")
            {
                draft.SystemDocuments.IdentityFrontPath = request.SavedFilePath;
                draft.SetStep(RegistrationStep.WaitingForIdentityBack);
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Success_IdentityFrontProcessed"]);
            }
            else
            {
                draft.SystemDocuments.IdentityBackPath = request.SavedFilePath;
                draft.SetStep(RegistrationStep.MergingDocuments);
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_MergingIdentityDocuments"]);

                var task = new MergePdfTask
                {
                    RegistrationKey = draft.RegistrationKey,
                    FrontFilePath = draft.SystemDocuments.IdentityFrontPath!,
                    BackFilePath = draft.SystemDocuments.IdentityBackPath!
                };

                var envelope = new WorkerTaskEnvelope
                {
                    CommandType = CommandType.MergePdf,
                    MergeTask = task
                };

                await _eventPublisher.PublishAsync("document-processing-queue", envelope);
            }
        }
        else if (request.DocumentType == "MergedIdentity")
        {
            if (draft.CurrentDocumentTargetName == "Vasi")
            {
                draft.Guardian.IdentityDocumentPath = request.SavedFilePath;
            }
            else
            {
                var child = draft.Children.FirstOrDefault(c => c.Name == draft.CurrentDocumentTargetName);
                if (child != null) child.IdentityDocumentPath = request.SavedFilePath;
            }

            // Checks whether any eligible children still require identity card verification before proceeding.
            var nextTarget = draft.GetNextPersonNeedingIdentity();
            if (nextTarget != null)
            {
                draft.SetDocumentTarget(nextTarget);
                draft.SetStep(RegistrationStep.WaitingForIdentityFront);

                string targetName = nextTarget == "Vasi" ? _localizer["Label_YourGuardianIdentity"].Value : _localizer["Label_ChildIdentity", nextTarget].Value;
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Success_IdentityProcessed_NextPerson", targetName].Value);
            }
            else
            {
                draft.SetStep(RegistrationStep.WaitingForBankAccountDocument);
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Prompt_BankAccountDocument"]);
            }
        }
        else if (request.DocumentType == "BankAccount")
        {
            draft.SystemDocuments.BankAccountDocumentPath = request.SavedFilePath;

            if (Iban.TryCreate(request.ExtractedIban, out var validIban) && validIban != null)
            {
                draft.Guardian.Iban = validIban;
                draft.SetStep(RegistrationStep.WaitingForQuestions);
                await _draftRepository.SaveAsync(draft, cancellationToken);

                await _notifier.SendMessageAsync(
                    draft.ChatId,
                    _localizer["Success_BankAccountDocumentAndIbanSaved", validIban.Value].Value);

                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey), cancellationToken);
                return;
            }

            draft.SetStep(RegistrationStep.WaitingForIban);
            await _draftRepository.SaveAsync(draft, cancellationToken);

            await _notifier.SendMessageAsync(
                draft.ChatId,
                _localizer["Success_BankAccountDocumentSaved_IbanRequired"].Value);
            return;
        }
        else
        {
            throw new NotSupportedException($"Belge türü {request.DocumentType} desteklenmiyor.");
        }

        await _draftRepository.SaveAsync(draft, cancellationToken);
    }
}
