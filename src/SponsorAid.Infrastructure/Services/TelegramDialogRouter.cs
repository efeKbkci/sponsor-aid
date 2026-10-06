using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

public class TelegramDialogRouter(IDraftStateRepository draftRepository, IMediator mediator, IUserNotifier userNotifier) : IDialogRouter
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUserNotifier _userNotifier = userNotifier;

    public async Task RouteIncomingMessageAsync(string payload, CancellationToken cancellationToken = default)
    {
        // JSON string'i ayrıştır (using ile RAM'den anında temizlenmesini sağlıyoruz)
        using var jsonData = JsonDocument.Parse(payload);
        var root = jsonData.RootElement;

        // 'message' objesini yakala
        if (!root.TryGetProperty("message", out var messageElement))
            return; // Message yoksa (örn: edited_message veya callback_query geldiyse) işlemi sonlandır

        var chatId = messageElement.GetProperty("chat").GetProperty("id").GetInt64();

        // Metin tabanlı mesajların işleneceği blok
        if (messageElement.TryGetProperty("text", out var textElement))
        {
            var text = textElement.GetString();

            if (text == "/start")
            {
                var registrationKey = await _mediator.Send(new StartRegistrationCommand(chatId));

                string welcomeMessage = $"👋 SponsorAid Sistemine Hoş Geldiniz!\n\n" +
                                        $"Kayıt süreciniz başarıyla başlatıldı. Başvuru takip kodunuz: <b>{registrationKey}</b>\n\n" +
                                        $"Lütfen sizden istenen belgeleri (Nüfus Kayıt Örneği, İkametgah vb.) PDF veya fotoğraf olarak bu ekrandan gönderiniz.";

                await _userNotifier.SendMessageAsync(chatId, welcomeMessage);
            }

            else if (text == "test")
            {
            }
        }
        // Binary (dosya veya fotoğraf) tabanlı mesajların işleneceği blok  
        else if (ExtractFileMetadata(messageElement) is var (fileId, fileSize, extension) && !string.IsNullOrEmpty(fileId))
        {
            // Kullanıcı bir dosya veya fotoğraf gönderdiğinde akış buraya düşer
            var drafts = await _draftRepository.ListAllAsync(chatId.ToString(), cancellationToken);
            var activeDraft = drafts.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

            if (activeDraft == null)
            {
                await _userNotifier.SendMessageAsync(chatId, "⚠️ Önce /start komutu ile bir kayıt başlatmalısınız.");
                return;
            }

            await _userNotifier.SendMessageAsync(chatId, "⏳ Belgeniz sisteme alınıyor, lütfen bekleyiniz...");
            /*
            // Checklist sırasına göre ilgili komutu tetikleme:
            if (string.IsNullOrEmpty(activeDraft.PopulationRegistryPath))
            {
                var command = new ProcessBinaryDocumentCommand(activeDraft.RegistrationKey, fileId, fileSize, extension);
                await _mediator.Send(command, cancellationToken);
            }
            else if (string.IsNullOrEmpty(activeDraft.IdentityFrontPath))
            {
                // Ön yüz komutu
            }
            else if (string.IsNullOrEmpty(activeDraft.IdentityBackPath))
            {
                // Arka yüz komutu
            }
            */
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