using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

public class TelegramDialogRouter(IDraftStateRepository draftRepository, IMediator mediator, IUserNotifier userNotifier) : IDialogRouter
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUserNotifier _userNotifier = userNotifier;

    public async Task RouteIncomingMessageAsync(string payload, CancellationToken cancellationToken = default)
    {
        using var jsonData = JsonDocument.Parse(payload);
        var root = jsonData.RootElement;
        if (!root.TryGetProperty("message", out var messageElement)) return;

        var chatId = messageElement.GetProperty("chat").GetProperty("id").GetInt64();
        string? text = messageElement.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
        var (fileId, fileSize, extension) = ExtractFileMetadata(messageElement);

        // KÜRESEL (GLOBAL) KOMUTLAR
        if (text == "/start")
        {
            // Sepeti (Draft) arka planda oluşturur ama Key'i henüz kullanıcıya söylemeyiz
            await _mediator.Send(new StartRegistrationCommand(chatId), cancellationToken);

            string welcomeMessage = $"👋 SponsorAid Sistemine Hoş Geldiniz!\n\n" +
                                    $"Lütfen kayıt işlemini başlatmak için öncelikle **Vasi (Başvuru Yapan) T.C. Kimlik Numarasını** 11 haneli olarak yazınız.";
            await _userNotifier.SendMessageAsync(chatId, welcomeMessage);
            return;
        }

        var drafts = await _draftRepository.ListAllAsync(chatId.ToString(), cancellationToken);
        var activeDraft = drafts.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

        if (activeDraft == null)
        {
            await _userNotifier.SendMessageAsync(chatId, "⚠️ Önce /start komutu ile bir kayıt başlatmalısınız.");
            return;
        }

        // KESİN VE PROFESYONEL DURUM YÖNETİMİ (EXPLICIT STATE)
        switch (activeDraft.CurrentStep)
        {
            case RegistrationStep.WaitingForGuardianId:
                if (string.IsNullOrEmpty(text))
                {
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen T.C. Kimlik numaranızı metin olarak yazınız.");
                    return;
                }

                try
                {
                    // 1. Value Object doğrulaması burada çalışacak (11 hane mi? Algoritma doğru mu? 0 ile mi başlıyor?)
                    activeDraft.ApplyUpdates(new() { { "GuardianIdNumber", text } });

                    // 2. Başarılıysa Nüfus Belgesi beklemeye al
                    activeDraft.SetStep(RegistrationStep.WaitingForPopulationRegistry);
                    await _draftRepository.SaveAsync(activeDraft, cancellationToken);

                    // 3. Kullanıcıya bilgi ver
                    string registrationCreatedMsg = $"✅ Kayıt başarıyla oluşturuldu!\n\n" +
                                                    $"🆔 <b>T.C. Kimlik No:</b> {text}\n" +
                                                    $"🔑 <b>Takip Kodunuz:</b> {activeDraft.RegistrationKey}\n\n" +
                                                    $"Lütfen e-Devlet'ten aldığınız <b>Vukuatlı Nüfus Kayıt Örneği</b> belgenizi PDF (veya net bir ekran görüntüsü) olarak gönderiniz.";

                    await _userNotifier.SendMessageAsync(chatId, registrationCreatedMsg);
                }
                catch (Exception ex)
                {
                    // Eğer uydurma bir TC veya eksik numara girildiyse (Value Object hata fırlattıysa) buraya düşer
                    await _userNotifier.SendMessageAsync(chatId, $"⚠️ Hata: {ex.Message} Lütfen geçerli bir T.C. Kimlik Numarası giriniz.");
                }
                break;

            case RegistrationStep.WaitingForPopulationRegistry:
                if (string.IsNullOrEmpty(fileId)) { 
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen nüfus belgenizi PDF veya fotoğraf olarak gönderiniz."); 
                    return; 
                }
                activeDraft.SetStep(RegistrationStep.ProcessingPopulationRegistry);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _userNotifier.SendMessageAsync(chatId, "⏳ Nüfus belgeniz okunuyor, lütfen bekleyiniz...");
                await _mediator.Send(new ProcessOfficialDocumentCommand(activeDraft.RegistrationKey, fileId, fileSize, extension, "PopulationRegistry"), cancellationToken);
                break;

            case RegistrationStep.WaitingForResidence:
                if (string.IsNullOrEmpty(fileId)) { 
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen ikametgah belgenizi gönderiniz."); 
                    return; 
                }
                activeDraft.SetStep(RegistrationStep.ProcessingResidence);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _userNotifier.SendMessageAsync(chatId, "⏳ İkametgah belgeniz işleniyor, lütfen bekleyiniz...");
                await _mediator.Send(new ProcessOfficialDocumentCommand(activeDraft.RegistrationKey, fileId, fileSize, extension, "Residence"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityFront:
                if (string.IsNullOrEmpty(fileId)) { 
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen kimlik belgenizin ön yüzünü gönderiniz."); 
                    return; 
                }
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityFront);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _userNotifier.SendMessageAsync(chatId, "⏳ Kimlik ön yüzünüz yapay zeka ile işleniyor...");

                // Parametre olarak "front" gönderiyoruz
                await _mediator.Send(new ProcessIdentityImageCommand(activeDraft.RegistrationKey, fileId, fileSize, extension, "front"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityBack:
                if (string.IsNullOrEmpty(fileId)) { 
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen kimlik belgenizin arka yüzünü gönderiniz."); 
                    return; 
                }
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityBack);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _userNotifier.SendMessageAsync(chatId, "⏳ Kimlik arka yüzünüz yapay zeka ile işleniyor...");

                // Parametre olarak "back" gönderiyoruz
                await _mediator.Send(new ProcessIdentityImageCommand(activeDraft.RegistrationKey, fileId, fileSize, extension, "back"), cancellationToken);
                break;

            // ASENKRON GÜVENLİK (Race Condition Koruması)
            case RegistrationStep.ProcessingPopulationRegistry:
            case RegistrationStep.ProcessingResidence:
            case RegistrationStep.ProcessingIdentityFront:
            case RegistrationStep.ProcessingIdentityBack:
            case RegistrationStep.MergingDocuments:
                await _userNotifier.SendMessageAsync(chatId, "⚠️ Şu an sistem bir önceki belgeniz üzerinde çalışıyor. Lütfen işlemin bitmesini bekleyiniz.");
                break;

            // METİN SORULARI AŞAMASI (Checklist Mantığı Devreye Girer)
            case RegistrationStep.WaitingForQuestions:
                if (!string.IsNullOrEmpty(fileId)) { await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen belge göndermeyin. Sadece sizden istenilen bilgiyi yazın."); return; }
                if (string.IsNullOrEmpty(text)) { await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen metin veya sayı giriniz."); return; }

                try
                {
                    if (activeDraft.Guardian.PhoneNumber == null)
                    {
                        activeDraft.ApplyUpdates(new() { { "GuardianPhoneNumber", text } });
                        await _userNotifier.SendMessageAsync(chatId, "✅ Telefon kaydedildi. Lütfen ailenizin toplam **Aylık Gelirini** sadece rakamla yazınız. (Örn: 17000)");
                    }
                    else if (activeDraft.FamilyFinancial.Income == null)
                    {
                        activeDraft.ApplyUpdates(new() { { "Income", text } });
                        await _userNotifier.SendMessageAsync(chatId, "✅ Gelir kaydedildi. Lütfen **Aylık Giderinizi** rakamla yazınız.");
                    }
                    else if (activeDraft.FamilyFinancial.Expense == null)
                    {
                        activeDraft.ApplyUpdates(new() { { "Expense", text } });
                        await _userNotifier.SendMessageAsync(chatId, "✅ Gider kaydedildi. Son olarak bursun yatırılacağı **IBAN numarasını** TR ile başlayarak yazınız.");
                    }
                    else if (activeDraft.Guardian.Iban == null)
                    {
                        activeDraft.ApplyUpdates(new() { { "GuardianIban", text } });
                        activeDraft.SetStep(RegistrationStep.Completed);
                        await _userNotifier.SendMessageAsync(chatId, "🎉 Harika! Tüm kayıt işlemleriniz başarıyla tamamlandı. Otomasyon sistemimiz dosyanızı hazırlıyor...");
                        // TODO: İleride FinalizeRegistrationCommand fırlatılacak
                    }
                    await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Value Object hataları (Örn: Geçersiz IBAN veya Telefon)
                    await _userNotifier.SendMessageAsync(chatId, $"⚠️ Hata: {ex.Message} Lütfen tekrar deneyiniz.");
                }
                break;

            case RegistrationStep.Completed:
                await _userNotifier.SendMessageAsync(chatId, "✅ Başvurunuz zaten tamamlanmıştır. Durumunuz değerlendirilmektedir.");
                break;
        }
    }

    private (string? FileId, long FileSize, string Extension) ExtractFileMetadata(JsonElement messageElement)
    {
        // 1. Döküman (Document) Kontrolü
        if (messageElement.TryGetProperty("document", out var documentElement))
        {
            var id = documentElement.GetProperty("file_id").GetString();
            var size = documentElement.GetProperty("file_size").GetInt64();
            var ext = "pdf"; // Varsayılan

            if (documentElement.TryGetProperty("file_name", out var fileNameElement))
            {
                ext = Path.GetExtension(fileNameElement.GetString())?.TrimStart('.') ?? ext;
            }
            return (id, size, ext);
        }

        // 2. Fotoğraf (Photo) Kontrolü
        if (messageElement.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
        {
            var largestPhoto = photoArray[photoArray.GetArrayLength() - 1];
            var id = largestPhoto.GetProperty("file_id").GetString();
            var size = largestPhoto.GetProperty("file_size").GetInt64();
            return (id, size, "jpg");
        }

        // Hiçbiri değilse boş dön
        return (null, 0, string.Empty);
    }
}