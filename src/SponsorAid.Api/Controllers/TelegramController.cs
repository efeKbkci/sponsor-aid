using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;

namespace SponsorAid.Api.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
public class TelegramController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserNotifier _notifier;
    private readonly IDraftStateRepository _draftRepository; // Kullanıcının aktif sepetini bulmak için ekledik

    public TelegramController(IMediator mediator, IUserNotifier notifier, IDraftStateRepository draftRepository)
    {
        _mediator = mediator;
        _notifier = notifier;
        _draftRepository = draftRepository;
    }

    [HttpPost]
    public async Task<IActionResult> HandleTelegramUpdate([FromBody] JsonElement updatePayload)
    {
        try
        {
            if (updatePayload.TryGetProperty("message", out var messageElement))
            {
                var chatId = messageElement.GetProperty("chat").GetProperty("id").GetInt64();

                // 1. METİN MESAJLARI (Komutlar)
                if (messageElement.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();

                    if (text == "/start" || text == "/basla")
                    {
                        var registrationKey = await _mediator.Send(new StartRegistrationCommand(chatId));

                        string welcomeMessage = $"👋 SponsorAid Sistemine Hoş Geldiniz!\n\n" +
                                                $"Kayıt süreciniz başarıyla başlatıldı. Başvuru takip kodunuz: <b>{registrationKey}</b>\n\n" +
                                                $"Lütfen sizden istenen belgeleri (Nüfus Kayıt Örneği, İkametgah vb.) PDF veya fotoğraf olarak bu ekrandan gönderiniz.";

                        await _notifier.SendMessageAsync(chatId, welcomeMessage);
                        return Ok();
                    }
                }

                // 2. BELGE VEYA FOTOĞRAF GÖNDERİMİ
                string? fileId = null;
                long fileSize = 0;
                string extension = "jpg"; // Fotoğraflar için varsayılan

                // Eğer PDF gibi bir döküman gönderdiyse
                if (messageElement.TryGetProperty("document", out var documentElement))
                {
                    fileId = documentElement.GetProperty("file_id").GetString();
                    fileSize = documentElement.GetProperty("file_size").GetInt64();

                    if (documentElement.TryGetProperty("file_name", out var fileNameElement))
                    {
                        var fileName = fileNameElement.GetString();
                        extension = Path.GetExtension(fileName)?.TrimStart('.') ?? "pdf";
                    }
                }
                // Eğer Galeriden Fotoğraf gönderdiyse (Telegram fotoğrafları boyut dizisi olarak gönderir)
                else if (messageElement.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
                {
                    // En yüksek çözünürlüklü olanı (dizinin son elemanını) alıyoruz
                    var largestPhoto = photoArray[photoArray.GetArrayLength() - 1];
                    fileId = largestPhoto.GetProperty("file_id").GetString();
                    fileSize = largestPhoto.GetProperty("file_size").GetInt64();
                    extension = "jpg";
                }

                // Dosya yakalandıysa işlemleri başlat
                if (!string.IsNullOrEmpty(fileId))
                {
                    // Kullanıcının mevcut sepetini bul
                    var drafts = await _draftRepository.ListAllAsync(chatId.ToString());
                    var activeDraft = drafts.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

                    if (activeDraft == null)
                    {
                        await _notifier.SendMessageAsync(chatId, "⚠️ Önce /start komutuna tıklayarak veya yazarak bir kayıt başlatmanız gerekmektedir.");
                        return Ok();
                    }

                    await _notifier.SendMessageAsync(chatId, "⏳ Belgeniz sisteme alınıyor, lütfen bekleyiniz...");

                    // Dosyayı İndir ve Worker için RabbitMQ'ya gönder
                    var command = new ProcessBinaryDocumentCommand(activeDraft.RegistrationKey, fileId, fileSize, extension);
                    await _mediator.Send(command);

                    // Not: Handler başarılı olursa kullanıcıya ekstra mesaj atmaya gerek yok, 
                    // çünkü Worker belgeyi okuyunca sonucu zaten RabbitMQ'dan bize dönecek ve biz o zaman müjdeyi vereceğiz.
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Webhook Hatası]: {ex.Message}");
            return Ok();
        }
    }
}