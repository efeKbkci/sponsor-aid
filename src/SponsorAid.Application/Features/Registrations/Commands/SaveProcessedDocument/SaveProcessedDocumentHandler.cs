using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models; // DTO'lar için
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Integration.Contracts;
using System.Text.Json;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

public class SaveProcessedDocumentCommandHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IEventPublisher eventPublisher,
    IEGovermentDocumentService eGovernment) : IRequestHandler<SaveProcessedDocumentCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier; // Telegram/WhatsApp fark etmeksizin mesaj atan servis
    private readonly IEventPublisher _eventPublisher = eventPublisher; // Event yayıncısı
    private readonly IEGovermentDocumentService _eGoverment = eGovernment; // e-Devlet belge indirme servisi

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
            await _notifier.SendMessageAsync(draft.ChatId, $"⚠️ Belgeniz işlenirken bir sorun oluştu: {request.ErrorMessage}. Lütfen daha net bir belge/fotoğraf gönderiniz.");
            return;
        }
        // GERİ BESLEME DÖNGÜSÜ: WORKER BİZE BARKOD GÖNDERDİYSE
        if (!string.IsNullOrEmpty(request.ExtractedBarcode))
        {
            await _notifier.SendMessageAsync(draft.ChatId, $"🔍 Belgenizde e-Devlet barkodu ({request.ExtractedBarcode}) tespit edildi. Orijinal belge sunucudan indiriliyor, lütfen bekleyiniz...");

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
                throw new BusinessRuleException("e-Devlet üzerinden belge indirilemedi. Lütfen belgenin tam PDF halini yüklemeyi deneyin.");
            }
        }
        // 2. BAŞARILI SONUÇLARIN DAĞITILMASI
        if (request.DocumentType == "PopulationRegistry")
        {
            var data = request.PopulationData!;
            draft.ApplyPopulationRegistryResult(data);
            draft.SystemDocuments.PopulationRegistryDocumentPath = request.SavedFilePath;

            // Nüfus bitti, İkametgaha geçiyoruz
            draft.SetStep(RegistrationStep.WaitingForResidence);

            // Silinecek
            var summaryBuilder = new System.Text.StringBuilder();
            summaryBuilder.AppendLine("✅ <b>Nüfus Kayıt Örneği Başarıyla İşlendi!</b>\n");
            summaryBuilder.AppendLine("📋 <b>Sisteme Kaydedilen Aile Tablosu:</b>");
            summaryBuilder.AppendLine($"👤 <b>Vasi:</b> {draft.Guardian.Name}");

            string fatherStatus = draft.Father.DeathDate.HasValue
                ? $"(Vefat: {draft.Father.DeathDate.Value:dd.MM.yyyy})"
                : "(Sağ)";
            summaryBuilder.AppendLine($"👨 <b>Baba:</b> {draft.Father.Name} {fatherStatus}\n");

            summaryBuilder.AppendLine($"👧👦 <b>Bursa Uygun Çocuklar ({draft.Children.Count} kişi):</b>");
            foreach (var child in draft.Children)
            {
                summaryBuilder.AppendLine($"  • {child.Name} (Doğum: {child.BirthDate:dd.MM.yyyy})");
            }

            summaryBuilder.AppendLine("\nŞimdi lütfen e-Devlet'ten aldığınız <b>İkametgah (Yerleşim Yeri) Belgenizi</b> PDF olarak gönderiniz.");

            await _notifier.SendMessageAsync(draft.ChatId, summaryBuilder.ToString());
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
            await _notifier.SendMessageAsync(draft.ChatId, "✅ İkametgah adresiniz başarıyla kaydedildi.\n\nŞimdi lütfen vasi için kimliğin ön yüzünü fotoğraflayıp gönderiniz.");
        }
        else if (request.DocumentType == "IdentityFront" || request.DocumentType == "IdentityBack")
        {
            var extractedTc = request.ExtractedTcNumber!;
            var expectedTc = draft.GetExpectedIdNumberForCurrentTarget();

            // 🚀 KURAL 1 & KURAL 2: Hem Ön hem Arka yüzdeki TC, Nüfustan gelen beklenen TC ile aynı olmak zorundadır.
            if (expectedTc == null || extractedTc != expectedTc)
            {
                throw new DocumentValidationException($"Yüklenen kimlikteki TC No ({extractedTc}) ile sıradaki kişinin ({draft.CurrentDocumentTargetName}) beklenen TC Numarası eşleşmiyor. Lütfen doğru kişinin kimliğini yüklediğinizden ve ön/arka yüzlerin aynı kimliğe ait olduğundan emin olun.");
            }

            if (request.DocumentType == "IdentityFront")
            {
                draft.SystemDocuments.IdentityFrontPath = request.SavedFilePath;
                draft.SetStep(RegistrationStep.WaitingForIdentityBack);
                await _notifier.SendMessageAsync(draft.ChatId, "✅ Kimlik Ön Yüzü alındı ve doğrulandı. Şimdi aynı kimliğin **ARKA YÜZ** fotoğrafını gönderiniz.");
            }
            else // IdentityBack
            {
                draft.SystemDocuments.IdentityBackPath = request.SavedFilePath;
                draft.SetStep(RegistrationStep.MergingDocuments);
                await _notifier.SendMessageAsync(draft.ChatId, "✅ Kimlik Arka Yüzü alındı ve doğrulandı. Belgeleriniz güvenli bir şekilde tek bir PDF'e birleştiriliyor, lütfen bekleyiniz...");

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

                string targetName = nextTarget == "Vasi" ? "Sizin (Vasi)" : $"{nextTarget} isimli çocuğunuzun";
                await _notifier.SendMessageAsync(draft.ChatId, $"✅ Kimlik başarıyla sisteme işlendi!\n\nŞimdi sıradaki kişiye geçiyoruz. Lütfen **{targetName}** kimlik ÖN YÜZ fotoğrafını gönderiniz.");
            }
            else
            {
                // Döngü bitti! Soru aşamasına geç.
                draft.SetStep(RegistrationStep.WaitingForQuestions);
                await _notifier.SendMessageAsync(draft.ChatId, "🎉 Harika! Tüm aile bireylerinin kimlik belgeleri başarıyla toplandı.\n\nSon birkaç sorum kaldı. Lütfen size ulaşabileceğimiz **Telefon Numaranızı** (Örn: 05551234567) metin olarak yazınız.");
            }
        }
        else
        {
            throw new NotSupportedException($"Belge türü {request.DocumentType} desteklenmiyor.");
        }

        await _draftRepository.SaveAsync(draft, cancellationToken);
    }
}