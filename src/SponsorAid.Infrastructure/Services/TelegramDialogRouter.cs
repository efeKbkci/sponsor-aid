using MediatR;
using Microsoft.Extensions.Logging;
using SponsorAid.Application.Features.Registrations.Commands.ProcessTextInput;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Parses raw Telegram webhook payloads and routes them to appropriate application commands.
/// </summary>
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

        // Processes callback button clicks as text commands associated with the source message.
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

        // Processes standard message updates containing text or attached documents.
        if (root.TryGetProperty("message", out var msg))
        {
            var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();

            // Routes document or photo uploads to binary input handler.
            var (fileId, fileSize, extension) = ExtractFileMetadata(msg);
            if (!string.IsNullOrEmpty(fileId))
            {
                await _mediator.Send(new ProcessBinaryInputCommand(chatId, fileId, fileSize, extension), cancellationToken);
                return;
            }

            // Extracts text from either direct message body or photo caption.
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
        // Extracts document file identifier and extracts extension from filename.
        if (messageElement.TryGetProperty("document", out var documentElement))
        {
            var id = documentElement.GetProperty("file_id").GetString();
            var size = documentElement.GetProperty("file_size").GetInt64();
            var ext = "pdf";

            if (documentElement.TryGetProperty("file_name", out var fileNameElement))
            {
                ext = Path.GetExtension(fileNameElement.GetString())?.TrimStart('.') ?? ext;
            }
            return (id, size, ext);
        }

        // Extracts largest photo entry, which Telegram positions at the end of the array.
        if (messageElement.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
        {
            var largestPhoto = photoArray[photoArray.GetArrayLength() - 1];
            var id = largestPhoto.GetProperty("file_id").GetString();
            var size = largestPhoto.GetProperty("file_size").GetInt64();
            return (id, size, "jpg");
        }

        return (null, 0, string.Empty);
    }
}
