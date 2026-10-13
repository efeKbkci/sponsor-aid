using MediatR;
using Microsoft.Extensions.Logging;
using SponsorAid.Application.Features.Registrations.Commands.ProcessTextInput;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using System.Text.Json;

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

        // 1. BUTON TIKLAMASI (Callback): Dosya olamaz, sadece metin/komut üretir
        if (root.TryGetProperty("callback_query", out var cb))
        {
            var chatId = cb.GetProperty("message").GetProperty("chat").GetProperty("id").GetInt64();
            var callbackMsgId = cb.GetProperty("message").GetProperty("message_id").GetInt32();
            var text = cb.GetProperty("data").GetString();

            if (!string.IsNullOrEmpty(text))
            {
                await _mediator.Send(new ProcessTextInputCommand(chatId, text, callbackMsgId), cancellationToken);
            }
            return;
        }

        // 2. NORMAL MESAJ: Ya dosyadır ya da metindir
        if (root.TryGetProperty("message", out var msg))
        {
            var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();

            // 2a. Dosya / Fotoğraf Kontrolü
            var (fileId, fileSize, extension) = ExtractFileMetadata(msg);
            if (!string.IsNullOrEmpty(fileId))
            {
                await _mediator.Send(new ProcessBinaryInputCommand(chatId, fileId, fileSize, extension), cancellationToken);
                return;
            }

            // 2b. Düz Metin veya Fotoğraf Altı Yazısı (Caption)
            var text = msg.TryGetProperty("text", out var t) ? t.GetString()
                     : msg.TryGetProperty("caption", out var c) ? c.GetString() : null;

            if (!string.IsNullOrEmpty(text))
            {
                await _mediator.Send(new ProcessTextInputCommand(chatId, text), cancellationToken);
            }
        }

        else
        {
            return;
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