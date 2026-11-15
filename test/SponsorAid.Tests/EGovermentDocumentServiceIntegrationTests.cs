using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SponsorAid.Application.Interfaces;
using SponsorAid.Infrastructure.Services;

namespace SponsorAid.Tests;

/// <summary>
/// Integration test suite for <see cref="EGovermentDocumentService"/>.
/// Verifies live HTTP communication, document download, and file persistence against the external e-Devlet portal.
/// </summary>
public class EGovermentDocumentServiceIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;

    public EGovermentDocumentServiceIntegrationTests()
    {
        // Replicate application dependency injection setup for isolated HTTP client testing
        var services = new ServiceCollection();

        // Match the exact client headers configured in the production API host to avoid bot filtering
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
            // Disable automatic cookies because session cookies are managed manually during document retrieval
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        // Register the production service implementation to test real HTTP communication
        services.AddTransient<IEGovermentDocumentService, EGovermentDocumentService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    // Categorized as integration test because it connects to an external government web service
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DownloadDocumentAsync_WithRealData_ShouldDownloadAndSavePdfFile()
    {
        // Arrange - Test inputs for manual verification against live e-Devlet service
        // Example barcode format: NV02-NOLP-3N0Y-N8O8
        string realBarcode = "Barcode";
        string realTcNumber = "Id";

        var service = _serviceProvider.GetRequiredService<IEGovermentDocumentService>();

        // Act - Send live request to e-Devlet document validation endpoint
        string savedFilePath = await service.DownloadDocumentAsync(realBarcode, realTcNumber);

        // Assert
        // Verify that the service returns a non-empty file path
        Assert.False(string.IsNullOrWhiteSpace(savedFilePath), "Servis dosya yolu döndürmedi.");

        // Verify that the file was written to disk
        Assert.True(File.Exists(savedFilePath), $"Dosya belirtilen yolda bulunamadı: {savedFilePath}");

        // Verify document size is above 5 KB to distinguish valid PDFs from HTML error pages or captchas
        var fileInfo = new FileInfo(savedFilePath);
        Assert.True(fileInfo.Length > 5000, $"İndirilen dosya boyutu çok küçük ({fileInfo.Length} bytes). e-Devlet hata sayfası (403/Captcha) dönmüş olabilir.");

        // Cleanup temporary downloaded file from disk after test execution
        if (File.Exists(savedFilePath))
        {
            File.Delete(savedFilePath);
        }
    }
}
