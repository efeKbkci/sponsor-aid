using System.Net;
using System.Text.RegularExpressions;
using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Infrastructure.Services;

public class EGovermentDocumentService : IEGovermentDocumentService
{
    private readonly string _storagePath;

    public EGovermentDocumentService()
    {
        // Dosyaların kaydedileceği fiziksel dizin
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> DownloadDocumentAsync(string barcode, string idNumber, CancellationToken cancellationToken = default)
    {
        // 1. Session ve Çerez (Cookie) yönetimi için İZOLE bir HttpClient oluşturuyoruz.
        // Bu sayede aynı anda belge indiren A ve B kullanıcısının e-Devlet çerezleri birbirine karışmaz.
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = true // allow_redirects=True işleminin C# karşılığı
        };
        using var client = new HttpClient(handler);

        // Temel başlıklar
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");

        string baseUrl = "https://www.turkiye.gov.tr/belge-dogrulama";

        // ---------------------------------------------------------
        // ADIM 1: İlk GET isteği - Çerezleri ve ilk token'ı al
        // ---------------------------------------------------------
        var response1 = await client.GetAsync(baseUrl, cancellationToken);
        var html1 = await response1.Content.ReadAsStringAsync(cancellationToken);
        string token1 = ExtractToken(html1, "1. Adım");

        // ---------------------------------------------------------
        // ADIM 2: İlk POST isteği - Barkod Numarasını Gönder
        // ---------------------------------------------------------
        var payload1 = new Dictionary<string, string>
        {
            { "sorgulananBarkod", barcode },
            { "token", token1 },
            { "btn", "Devam Et" }
        };
        var response2 = await client.PostAsync($"{baseUrl}?submit", new FormUrlEncodedContent(payload1), cancellationToken);
        var html2 = await response2.Content.ReadAsStringAsync(cancellationToken);
        string token2 = ExtractToken(html2, "2. Adım");

        // ---------------------------------------------------------
        // ADIM 3: İkinci POST isteği - T.C. Kimlik Gönder
        // ---------------------------------------------------------
        var payload2 = new Dictionary<string, string>
        {
            { "ikinciAlan", idNumber },
            { "token", token2 },
            { "btn", "Devam Et" }
        };
        var response3 = await client.PostAsync($"{baseUrl}?islem=dogrulama&submit", new FormUrlEncodedContent(payload2), cancellationToken);
        var html3 = await response3.Content.ReadAsStringAsync(cancellationToken);
        string token3 = ExtractToken(html3, "3. Adım");

        // ---------------------------------------------------------
        // ADIM 4: Üçüncü POST isteği - Onay Kutusu Gönder
        // ---------------------------------------------------------
        var payload3 = new Dictionary<string, string>
        {
            { "chkOnay", "1" },
            { "token", token3 },
            { "btn", "Devam Et" }
        };
        await client.PostAsync($"{baseUrl}?islem=onay&submit", new FormUrlEncodedContent(payload3), cancellationToken);

        // ---------------------------------------------------------
        // ADIM 5: Son GET İsteği - Belgeyi İndir
        // ---------------------------------------------------------
        string downloadUrl = $"{baseUrl}?belge=goster&goster=1&display=display";
        var response5 = await client.GetAsync(downloadUrl, cancellationToken);

        if (response5.IsSuccessStatusCode)
        {
            var fileBytes = await response5.Content.ReadAsByteArrayAsync(cancellationToken);

            // Basit bir PDF doğrulama kontrolü (Boyutu 1000 bytetan büyük mü?)
            if (fileBytes.Length > 1000)
            {
                var fileName = $"edevlet_{barcode}_{Guid.NewGuid():N}.pdf";
                var fullPath = Path.Combine(_storagePath, fileName);

                await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);
                return fullPath; // İndirilen tertemiz PDF'in adresini Worker'a iletilmek üzere geri dön
            }
        }

        throw new BusinessRuleException("Error_EGovernmentDocumentDownloadFailed");
    }

    // BeautifulSoup yerine Regex kullanarak HTML içinden data-token değerini saniyenin binde biri hızında çeken yardımcı metot
    // Sadece <body> etiketi içindeki data-token değerini arar (Python'daki soup.find('body') mantığı)
    private static string ExtractToken(string html, string step)
    {
        // <body ile başlayan, data-token=" kısmına kadar olan her şeyi es geçen ve tırnak içini yakalayan Regex
        var match = Regex.Match(html, @"<body[^>]*data-token=[""']([^""']+)[""']", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            // match.Groups[1].Value bize doğrudan süslü parantezleriyle birlikte 
            return match.Groups[1].Value;
        }

        throw new BusinessRuleException("Error_EGovernmentSecurityTokenNotFound", step);
    }
}