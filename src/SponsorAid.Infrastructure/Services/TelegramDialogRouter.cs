using MediatR;
using Microsoft.Extensions.Logging;
using SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;
using SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Constants;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SponsorAid.Infrastructure.Services;

public class TelegramDialogRouter(
    IDraftStateRepository draftRepository, IMediator mediator, 
    IUserNotifier userNotifier, ILogger<TelegramDialogRouter> logger) : IDialogRouter
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUserNotifier _userNotifier = userNotifier;
    private readonly ILogger<TelegramDialogRouter> _logger = logger;

    public async Task RouteIncomingMessageAsync(string payload, CancellationToken cancellationToken = default)
    {
        using var jsonData = JsonDocument.Parse(payload);
        var root = jsonData.RootElement;

        long chatId = 0;
        string? text = null;
        string? fileId = null;
        long fileSize = 0;
        string extension = string.Empty;
        int? callbackMessageId = null; // 🚀 YENİ: Tıklanan butonun mesaj ID'si

        // 1. KULLANICI BUTONA MI TIKLADI? (Callback Query)
        if (root.TryGetProperty("callback_query", out var callbackQuery))
        {
            chatId = callbackQuery.GetProperty("message").GetProperty("chat").GetProperty("id").GetInt64();
            callbackMessageId = callbackQuery.GetProperty("message").GetProperty("message_id").GetInt32(); // 🚀 Mesajı yakala
            text = callbackQuery.GetProperty("data").GetString();
        }
        // 2. KULLANICI METİN/BELGE Mİ GÖNDERDİ? (Message)
        else if (root.TryGetProperty("message", out var messageElement))
        {
            chatId = messageElement.GetProperty("chat").GetProperty("id").GetInt64();
            text = messageElement.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;

            var extractedMetadata = ExtractFileMetadata(messageElement);
            fileId = extractedMetadata.FileId;
            fileSize = extractedMetadata.FileSize;
            extension = extractedMetadata.Extension;
        }
        else
        {
            return;
        }

        // KÜRESEL (GLOBAL) KOMUTLAR
        if (text == Commands.StartBot)
        {
            string welcomeMessage = $"SponsorAid Sistemine Hoş Geldiniz!\n\n" +
                                    $"Lütfen yapmak istediğiniz işlemi belirtiniz.";
            await _userNotifier.SendMessageAsync(chatId, welcomeMessage);
            return;
        }
        else if (text == Commands.NewRegistration)
        {
            // Sepeti (Draft) arka planda oluşturur ama Key'i henüz kullanıcıya söylemeyiz
            await _mediator.Send(new StartRegistrationCommand(chatId), cancellationToken);

            string newRegistrationMessage = $"✅ Yeni bir kayıt başlatıldı!\n\n" +
                                            $"Lütfen vasiye ait T.C. Kimlik numarasını metin olarak yazınız.";
            await _userNotifier.SendMessageAsync(chatId, newRegistrationMessage);
            return;
        }
        else if (Regex.Match(text?.Trim() ?? string.Empty, @"^/continue(?:\s+([a-zA-Z0-9]+))?$", RegexOptions.IgnoreCase) is { Success: true } match)
        {
            string? guidPart = match.Groups[1].Success ? match.Groups[1].Value.Trim().ToUpper() : null;

            if (string.IsNullOrEmpty(guidPart))
            {
                await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen devam etmek istediğiniz kaydın takip kodunu giriniz.\nÖrnek: <code>/continue ABC123</code>");
                return;
            }

            // 🚀 ChatId ile birlikte Handler'a gönder
            await _mediator.Send(new ContinueRegistrationCommand(guidPart, chatId), cancellationToken);
            return;
        }

        var activeDraft = await _draftRepository.GetActiveByChatIdAsync(chatId, cancellationToken);

        if (activeDraft == null)
        {
            await _userNotifier.SendMessageAsync(chatId, "⚠️ Önce /start komutu ile bir kayıt başlatmalısınız.");
            return;
        }

        // 🚀 SORU GÜNCELLEME KOMUTU (/update <ANAHTAR>)
        if (Regex.Match(text?.Trim() ?? string.Empty, @"^/update(?:\s+([a-zA-Z0-9_]+))?$", RegexOptions.IgnoreCase) is { Success: true } updateMatch)
        {
            string? keyPart = updateMatch.Groups[1].Success ? updateMatch.Groups[1].Value.Trim() : null;

            if (string.IsNullOrEmpty(keyPart))
            {
                await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen güncellemek istediğiniz sorunun anahtarını belirtiniz.\nÖrnek: <code>/update meslek</code> veya <code>/update iban</code>");
                return;
            }

            await _mediator.Send(new UpdateQuestionAnswerCommand(activeDraft.RegistrationKey, keyPart, chatId), cancellationToken);
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
                catch (BusinessRuleException ex)
                {
                    // Eğer uydurma bir TC veya eksik numara girildiyse (Value Object hata fırlattıysa) buraya düşer
                    await _userNotifier.SendMessageAsync(chatId, $"⚠️ Hata: {ex.Message} Lütfen geçerli bir T.C. Kimlik Numarası giriniz.");
                }
                catch (Exception ex)
                {
                    // Beklenmeyen bir hata olursa logla ve kullanıcıya genel bir mesaj ver
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Bir hata oluştu. Lütfen tekrar deneyiniz.");
                    _logger.LogError(ex, "TC Kimlik kaydı sırasında beklenmeyen bir hata oluştu. ChatId: {ChatId}, RegistrationKey: {RegistrationKey}", chatId, activeDraft.RegistrationKey);                }
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

            case RegistrationStep.WaitingForQuestions:
                if (!string.IsNullOrEmpty(fileId))
                {
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen belge göndermeyin. Sadece seçeneklere tıklayın veya metin yazın.");
                    return;
                }
                if (string.IsNullOrEmpty(text))
                {
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Lütfen bir seçim yapınız veya yazınız.");
                    return;
                }

                // 🚀 BÜYÜK TEMİZLİK: Sadece Handler'ı çağır! 
                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(activeDraft.RegistrationKey, text, callbackMessageId), cancellationToken);
                break;

            case RegistrationStep.WaitingForConfirmation:
                if (text == "confirm_registration")
                {
                    // Buton mesajını yeşil tike çevirip butonları kaldırıyoruz
                    if (callbackMessageId.HasValue)
                    {
                        await _userNotifier.UpdateMessageAsAnsweredAsync(
                            chatId,
                            callbackMessageId.Value,
                            "📋 <b>Başvuru Özeti</b>",
                            "Bilgiler onaylandı."
                        );
                    }

                    await _userNotifier.SendMessageAsync(chatId, "⏳ Başvurunuz doğrulanıyor ve kaydediliyor, lütfen bekleyiniz...");

                    // 🚀 Asıl kayıt tamamlama, temizlik ve takip tablosu güncellemesi burada tetiklenir:
                    await _mediator.Send(new FinalizeRegistrationCommand(activeDraft.RegistrationKey), cancellationToken);
                }
                else
                {
                    await _userNotifier.SendMessageAsync(chatId, "⚠️ Başvuruyu tamamlamak için lütfen yukarıdaki <b>'Bilgileri Onaylıyorum'</b> butonuna tıklayınız veya düzeltmek istediğiniz bir alan varsa <code>/update &lt;alan&gt;</code> komutunu kullanınız.");
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