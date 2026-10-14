using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SponsorAid.Application.Interfaces;
using SponsorAid.Infrastructure.Services;

namespace SponsorAid.Tests;

public class EGovermentDocumentServiceIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;

    public EGovermentDocumentServiceIntegrationTests()
    {
        // 1. Gerçek uygulamanın Dependency Injection (DI) konteynerini simüle ediyoruz
        var services = new ServiceCollection();

        // Uygulamanın Program.cs dosyasındaki ayarların birebir aynısı:
        services.AddHttpClient("EDevletClient", client =>
        {
            client.BaseAddress = new Uri("https://www.turkiye.gov.tr/");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");

            client.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\"");
            client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            UseCookies = false, // Çerezlerin manuel yönetimi için kapalı
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        // Gerçek servisimizi DI konteynerine ekliyoruz
        services.AddTransient<IEGovermentDocumentService, EGovermentDocumentService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    [Trait("Category", "Integration")] // Bu testi kategorize ettik
    public async Task DownloadDocumentAsync_WithRealData_ShouldDownloadAndSavePdfFile()
    {
        // Arrange (Hazırlık) - BURAYI GERÇEK VERİLERLE DOLDURUN
        string realBarcode = "Barcode"; // Örn: NV02-NOLP-3N0Y-N8O8
        string realTcNumber = "Id";

        var service = _serviceProvider.GetRequiredService<IEGovermentDocumentService>();

        // Act (Eylem) - Gerçek e-Devlet sunucularına bağlanılıyor
        string savedFilePath = await service.DownloadDocumentAsync(realBarcode, realTcNumber);

        // Assert (Doğrulama)

        // 1. Dosya yolu boş dönmemeli
        Assert.False(string.IsNullOrWhiteSpace(savedFilePath), "Servis dosya yolu döndürmedi.");

        // 2. Fiziksel dosya diskte gerçekten oluşmuş olmalı
        Assert.True(File.Exists(savedFilePath), $"Dosya belirtilen yolda bulunamadı: {savedFilePath}");

        // 3. İndirilen PDF dosyasının boyutu (Beklenen: Hata sayfası değilse > 5000 byte olmalı)
        var fileInfo = new FileInfo(savedFilePath);
        Assert.True(fileInfo.Length > 5000, $"İndirilen dosya boyutu çok küçük ({fileInfo.Length} bytes). e-Devlet hata sayfası (403/Captcha) dönmüş olabilir.");

        // TearDown (Temizlik) - Test başarılı olduysa diski çöpe boğmamak için indirilen PDF'i sil
        if (File.Exists(savedFilePath))
        {
            File.Delete(savedFilePath);
        }
    }
}