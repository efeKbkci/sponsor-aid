using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

public class TelegramUserNotifier : IUserNotifier
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;

    public TelegramUserNotifier(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Tıpkı FileStorage servisindeki gibi güvenli token okuma yapımız.
        _botToken = configuration["TELEGRAM_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
            ?? throw new ArgumentNullException("TELEGRAM_TOKEN eksik! Lütfen .env dosyasını kontrol edin.");
    }

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
}