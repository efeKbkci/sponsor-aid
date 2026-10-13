using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

public class TelegramUserNotifier(HttpClient httpClient, IConfiguration configuration) : IUserNotifier
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _botToken = configuration["TELEGRAM_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
            ?? throw new ArgumentNullException("TELEGRAM_TOKEN eksik! Lütfen .env dosyasını kontrol edin.");

    public async Task SendMessageAsync(long chatId, string message)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        // Telegram'ın bizden beklediği JSON şablonu
        var payload = new
        {
            chat_id = chatId,
            text = message,
            parse_mode = "HTML" // Metinlerde <b>kalın</b> veya <i>italik</i> HTML etiketlerini kullanabilmek için ekledik.
        };

        var jsonContent = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);

            // Eğer Telegram'dan 200 OK gelmezse (Örn: Kullanıcı botu engellemişse) hatayı fırlatır.
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Kullanıcıya mesaj atılamaması, asıl işlemleri (kaydı tamamlamayı) durdurmamalı.
            // Bu yüzden hatayı konsola/log sistemine yazdırıp sessizce yutmak, sistemin devamlılığı için en güvenli yoldur.
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

        // JSON yerine form-data kullanıyoruz (Dosya yükleme standartı)
        using var form = new MultipartFormDataContent();

        // 1. Chat Id
        form.Add(new StringContent(chatId.ToString()), "chat_id");

        // 2. Varsa Alt Yazı (Caption)
        if (!string.IsNullOrEmpty(caption))
        {
            form.Add(new StringContent(caption), "caption");
        }

        // 3. Dosyanın Kendisi
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var fileContent = new StreamContent(fileStream);

        // Dosya türünü belirtiyoruz (Varsayılan olarak PDF veya binary kabul edilebilir)
        var contentType = Path.GetExtension(filePath).ToLower() == ".pdf" ? "application/pdf" : "application/octet-stream";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // Form'a eklerken dosyanın adını da (Path.GetFileName) gönderiyoruz ki kullanıcı Telegram'da o isimle görsün
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

        // Butonları Telegram'ın beklediği Inline Keyboard JSON formatına dönüştürüyoruz
        var inlineKeyboard = new List<List<object>>();
        foreach (var option in options)
        {
            // Her seçeneği alt alta (tek sütun) bir buton olarak ekliyoruz
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
            // reply_markup parametresini bilerek göndermiyoruz, böylece butonlar tamamen siliniyor.
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(url, content);
    }
    public async Task RemoveButtonsAsync(long chatId, int messageId)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/editMessageReplyMarkup";

        // editMessageReplyMarkup metoduna reply_markup göndermediğimizde (veya boş klavye gönderdiğimizde) 
        // Telegram mesaja hiç dokunmaz, sadece altındaki butonları tamamen siler.
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