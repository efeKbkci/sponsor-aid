using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

public class TelegramLocalFileStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _storagePath;

    public TelegramLocalFileStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Önce Configuration'dan, bulamazsa doğrudan Sistem Çevre Değişkenlerinden okumayı dener.
        _botToken = configuration["TELEGRAM_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
            ?? throw new ArgumentNullException("TELEGRAM_TOKEN eksik! Lütfen .env dosyasını kontrol edin.");

        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> DownloadAndSaveAsync(string fileId, string extension)
    {
        // 1. TELEGRAM'DAN DOSYA YOLUNU (file_path) İSTE
        // Telegram sadece FileId vererek doğrudan indirmeye izin vermez. Önce o FileId'nin tam yolunu sormalıyız.
        var getFileUrl = $"https://api.telegram.org/bot{_botToken}/getFile?file_id={fileId}";

        var getFileResponse = await _httpClient.GetAsync(getFileUrl);
        getFileResponse.EnsureSuccessStatusCode();

        var content = await getFileResponse.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(content);

        var filePath = jsonDocument.RootElement
            .GetProperty("result")
            .GetProperty("file_path")
            .GetString();

        if (string.IsNullOrEmpty(filePath))
        {
            throw new Exception("Telegram'dan dosya indirme yolu (file_path) alınamadı.");
        }

        // 2. DOSYAYI FİZİKSEL OLARAK İNDİR
        var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";
        var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);

        // 3. SUNUCU DİSKİNE KAYDET
        // Çakışmaları önlemek için dosyaya benzersiz bir Guid isim veriyoruz.
        var fileName = $"{Guid.NewGuid():N}.{extension.TrimStart('.')}";
        var fullPath = Path.Combine(_storagePath, fileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes);

        // 4. TAM YOLU DÖN (Worker bu yolu alıp diski okuyacak)
        return fullPath;
    }

    public void DeleteFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            // Dosya silinemezse loglanabilir, ana akışı bozmamalı
            Console.WriteLine($"[Uyarı] Dosya silinemedi: {filePath}. Hata: {ex.Message}");
        }
    }
}