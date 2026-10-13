using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Integration.Contracts;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

public class SaveProcessedDocumentCommandHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IEventPublisher eventPublisher,
    IEGovermentDocumentService eGovernment,
    IRegistrationTrackingRepository trackingRepository,
    IStringLocalizer<BotMessages> localizer
    ) : IRequestHandler<SaveProcessedDocumentCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier; // Telegram/WhatsApp fark etmeksizin mesaj atan servis
    private readonly IEventPublisher _eventPublisher = eventPublisher; // Event yayıncısı
    private readonly IEGovermentDocumentService _eGoverment = eGovernment; // e-Devlet belge indirme servisi
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository; // Takip tablosu servisi
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(SaveProcessedDocumentCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null) throw new NullReferenceException($"Draft with key {request.RegistrationKey} not found.");

        // 1. KİLİTLENMEYİ (DEADLOCK) ÇÖZMEK İÇİN STATE REVERT (GERİ ALMA) MANTIĞI
        if (!request.IsSuccess)
        {
            if (draft.CurrentStep == RegistrationStep.ProcessingPopulationRegistry)
                draft.SetStep(RegistrationStep.WaitingForPopulationRegistry);
            else if (draft.CurrentStep == RegistrationStep.ProcessingResidence)
                draft.SetStep(RegistrationStep.WaitingForResidence);
            else if (draft.CurrentStep == RegistrationStep.ProcessingIdentityFront)
                draft.SetStep(RegistrationStep.WaitingForIdentityFront);
            else if (draft.CurrentStep == RegistrationStep.ProcessingIdentityBack || draft.CurrentStep == RegistrationStep.MergingDocuments)
                draft.SetStep(RegistrationStep.WaitingForIdentityBack);

            await _draftRepository.SaveAsync(draft, cancellationToken);
            await _notifier.SendMessageAsync(draft.ChatId, _localizer["Error_DocumentProcessingFailed", request.ErrorMessage ?? string.Empty].Value);
            return;
        }
        // GERİ BESLEME DÖNGÜSÜ: WORKER BİZE BARKOD GÖNDERDİYSE
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
                    FilePath = downloadedPdfPath, // 🚀 Yeni indirdiğimiz TERTEMİZ PDF'in yolu
                    DocumentType = request.DocumentType == "PopulationRegistry"
                                    ? DocumentType.PopulationRegistry
                                    : DocumentType.Residence
                };

                var envelope = new WorkerTaskEnvelope
                {
                    CommandType = CommandType.ProcessOfficialDoc,
                    OfficialDocTask = task
                };

                // Orijinal belgeyi RabbitMQ'ya tekrar atıyoruz! 
                // State'i (Processing) değiştirmemize gerek yok, zaten "İşleniyor" durumunda.
                await _eventPublisher.PublishAsync("document-processing-queue", envelope);
                return; // 🚀 Metodu burada kesiyoruz. Worker temiz PDF'i okuyup döndüğünde alttaki kodlar çalışacak.
            }
            catch (Exception ex)
            {
                // İndirme başarısız olursa durumu "Bekliyor"a çek ve hata fırlat
                draft.RevertProcessingState();
                await _draftRepository.SaveAsync(draft, cancellationToken);
                throw new BusinessRuleException("Error_EGovernmentDocumentDownloadFailed");
            }
        }
        // 2. BAŞARILI SONUÇLARIN DAĞITILMASI
        if (request.DocumentType == "PopulationRegistry")
        {
            var data = request.PopulationData!;
            draft.ApplyPopulationRegistryResult(data);
            draft.SystemDocuments.PopulationRegistryDocumentPath = request.SavedFilePath;
           
            // Vasi adı belgeden okunduğu an takip tablosunda ismi güncelliyoruz:
            if (!string.IsNullOrEmpty(draft.Guardian.Name))
            {
                await _trackingRepository.UpdateGuardianNameAsync(draft.RegistrationKey, draft.Guardian.Name, cancellationToken);
            }
            // Nüfus bitti, İkametgaha geçiyoruz
            draft.SetStep(RegistrationStep.WaitingForResidence);

            // Kullanıcıya özet mesajı gönderiyoruz
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

            // İkametgah bitti, ilk Kimlik hedefini belirliyoruz
            var targetName = draft.GetNextPersonNeedingIdentity();
            draft.SetDocumentTarget(targetName);
            draft.SetStep(RegistrationStep.WaitingForIdentityFront);

            // Kullanıcıya özet mesajı gönderiyoruz
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

            // 🚀 KURAL 1 & KURAL 2: Hem Ön hem Arka yüzdeki TC, Nüfustan gelen beklenen TC ile aynı olmak zorundadır.
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
            else // IdentityBack
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
            // A) BİRLEŞTİRİLEN KİMLİĞİ DOĞRU KİŞİYE KAYDET (Çoklu Kimlik Algoritması)
            if (draft.CurrentDocumentTargetName == "Vasi")
            {
                draft.Guardian.IdentityDocumentPath = request.SavedFilePath;
            }
            else
            {
                var child = draft.Children.FirstOrDefault(c => c.Name == draft.CurrentDocumentTargetName);
                if (child != null) child.IdentityDocumentPath = request.SavedFilePath;
            }

            // B) SIRADA BAŞKA KİMLİK VAR MI DİYE KONTROL ET (Döngü)
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
                // Döngü bitti! Soru aşamasına geç.
                draft.SetStep(RegistrationStep.WaitingForQuestions);
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Success_AllIdentityDocumentsCollected"]);
            }
        }
        else
        {
            throw new NotSupportedException($"Belge türü {request.DocumentType} desteklenmiyor.");
        }

        await _draftRepository.SaveAsync(draft, cancellationToken);
    }
}