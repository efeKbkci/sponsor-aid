using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Downloads and stores media files received from Telegram bot chats on the local file system.
/// </summary>
public class TelegramLocalFileStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _storagePath;

    public TelegramLocalFileStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Reads the Telegram bot token from configuration with environment variable fallback.
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
        // Requests file path metadata from Telegram API before downloading the actual binary.
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

        // Downloads binary file contents from Telegram file endpoint.
        var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";
        var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);

        // Generates unique filename to prevent overwriting files with identical original names.
        var fileName = $"{Guid.NewGuid():N}.{extension.TrimStart('.')}";
        var fullPath = Path.Combine(_storagePath, fileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes);

        // Returns absolute file path so background workers can read the stored file.
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
            // Swallows file deletion failure to avoid disrupting primary application workflows.
            Console.WriteLine($"[Uyarı] Dosya silinemedi: {filePath}. Hata: {ex.Message}");
        }
    }
}
