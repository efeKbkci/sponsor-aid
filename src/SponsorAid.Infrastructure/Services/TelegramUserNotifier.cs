using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Sends text notifications, documents, and interactive keyboard options to users through the Telegram Bot API.
/// </summary>
public class TelegramUserNotifier(HttpClient httpClient, IConfiguration configuration) : IUserNotifier
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _botToken = configuration["TELEGRAM_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
            ?? throw new ArgumentNullException("TELEGRAM_TOKEN eksik! Lütfen .env dosyasını kontrol edin.");

    public async Task SendMessageAsync(long chatId, string message)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = message,
            // HTML parse mode enables basic formatting tags such as bold and italic.
            parse_mode = "HTML"
        };

        var jsonContent = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Catches notification errors so external delivery failures do not block the core business transaction.
            Console.WriteLine($"[HATA] Telegram mesajı gönderilemedi (ChatId: {chatId}). Hata: {ex.Message}");
        }
    }

    public async Task SendDocumentAsync(long chatId, string filePath, string? caption = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Gönderilmek istenen dosya diskte bulunamadı.", filePath);
        }

        var url = $"https://api.telegram.org/bot{_botToken}/sendDocument";

        // Uses multipart form data for uploading binary documents to Telegram.
        using var form = new MultipartFormDataContent();

        form.Add(new StringContent(chatId.ToString()), "chat_id");

        if (!string.IsNullOrEmpty(caption))
        {
            form.Add(new StringContent(caption), "caption");
        }

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var fileContent = new StreamContent(fileStream);

        // Sets media type based on file extension, defaulting to binary stream.
        var contentType = Path.GetExtension(filePath).ToLower() == ".pdf" ? "application/pdf" : "application/octet-stream";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // Injects filename so Telegram presents a recognizable document name in chat.
        form.Add(fileContent, "document", Path.GetFileName(filePath));

        try
        {
            var response = await _httpClient.PostAsync(url, form, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HATA] Telegram belgesi gönderilemedi (ChatId: {chatId}). Hata: {ex.Message}");
        }
    }

    public async Task SendQuestionWithButtonsAsync(long chatId, string question, Dictionary<string, string> options)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        // Formats interactive options into a single-column Telegram inline keyboard.
        var inlineKeyboard = new List<List<object>>();
        foreach (var option in options)
        {
            inlineKeyboard.Add(new List<object> { new { text = option.Value, callback_data = option.Key } });
        }

        var payload = new
        {
            chat_id = chatId,
            text = question,
            parse_mode = "HTML",
            reply_markup = new { inline_keyboard = inlineKeyboard }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HATA] Butonlu mesaj gönderilemedi: {ex.Message}");
        }
    }

    public async Task UpdateMessageAsAnsweredAsync(long chatId, int messageId, string originalQuestion, string selectedOptionLabel)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/editMessageText";

        var payload = new
        {
            chat_id = chatId,
            message_id = messageId,
            parse_mode = "HTML",
            text = $"{originalQuestion}\n\n✅ <b>Seçiminiz:</b> {selectedOptionLabel}"
            // Omitting reply_markup automatically removes inline buttons from the edited message.
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(url, content);
    }

    public async Task RemoveButtonsAsync(long chatId, int messageId)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/editMessageReplyMarkup";

        // Removing reply markup without altering text clears buttons after user selection.
        var payload = new
        {
            chat_id = chatId,
            message_id = messageId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HATA] Mesaj butonları kaldırılamadı: {ex.Message}");
        }
    }
}
