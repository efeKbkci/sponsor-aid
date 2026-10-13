using System.Net;
using System.Text.RegularExpressions;
using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Infrastructure.Services;

public class EGovermentDocumentService : IEGovermentDocumentService
{
    private readonly string _storagePath;
    private readonly IHttpClientFactory _httpClientFactory;

    public EGovermentDocumentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> DownloadDocumentAsync(string barcode, string idNumber, CancellationToken cancellationToken = default)
    {
        // Havuzdan (pool) bağlantı al. (Performans dostu)
        var client = _httpClientFactory.CreateClient("EDevletClient");

        // Bu işlem (session) için İZOLE çerez kutusu (Session karışmasını önler)
        var cookieContainer = new CookieContainer();
        var baseUri = new Uri("https://www.turkiye.gov.tr/");

        // ---------------------------------------------------------
        // ADIM 1: İlk GET isteği - Çerezleri ve ilk token'ı al
        // ---------------------------------------------------------
        var response1 = await SendRequestAsync(client, HttpMethod.Get, "belge-dogrulama", null, cookieContainer, baseUri, cancellationToken);
        var html1 = await response1.Content.ReadAsStringAsync(cancellationToken);
        string token1 = ExtractToken(html1, "1. Adım");

        await HumanizeDelayAsync(cancellationToken); // Bot korumasına takılmamak için bekle

        // ---------------------------------------------------------
        // ADIM 2: İlk POST isteği - Barkod Numarasını Gönder
        // ---------------------------------------------------------
        var payload1 = new Dictionary<string, string> { { "sorgulananBarkod", barcode }, { "token", token1 }, { "btn", "Devam Et" } };
        var response2 = await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?submit", payload1, cookieContainer, baseUri, cancellationToken);
        var html2 = await response2.Content.ReadAsStringAsync(cancellationToken);
        string token2 = ExtractToken(html2, "2. Adım");

        await HumanizeDelayAsync(cancellationToken);

        // ---------------------------------------------------------
        // ADIM 3: İkinci POST isteği - T.C. Kimlik Gönder
        // ---------------------------------------------------------
        var payload2 = new Dictionary<string, string> { { "ikinciAlan", idNumber }, { "token", token2 }, { "btn", "Devam Et" } };
        var response3 = await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?islem=dogrulama&submit", payload2, cookieContainer, baseUri, cancellationToken);
        var html3 = await response3.Content.ReadAsStringAsync(cancellationToken);
        string token3 = ExtractToken(html3, "3. Adım");

        await HumanizeDelayAsync(cancellationToken);

        // ---------------------------------------------------------
        // ADIM 4: Üçüncü POST isteği - Onay Kutusu Gönder
        // ---------------------------------------------------------
        var payload3 = new Dictionary<string, string> { { "chkOnay", "1" }, { "token", token3 }, { "btn", "Devam Et" } };
        await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?islem=onay&submit", payload3, cookieContainer, baseUri, cancellationToken);

        await HumanizeDelayAsync(cancellationToken);

        // ---------------------------------------------------------
        // ADIM 5: Son GET İsteği - Belgeyi İndir
        // ---------------------------------------------------------
        var response5 = await SendRequestAsync(client, HttpMethod.Get, "belge-dogrulama?belge=goster&goster=1&display=display", null, cookieContainer, baseUri, cancellationToken);

        if (response5.IsSuccessStatusCode)
        {
            var fileBytes = await response5.Content.ReadAsByteArrayAsync(cancellationToken);

            // PDF boyutu makul mü? (Örn. e-Devlet 403 dönmüşse HTML sayfası gelir ve boyutu küçüktür)
            if (fileBytes.Length > 5000) // 1000 byte'tan 5000'e çektik, HTML sayfaları genelde 3-4 kb olur.
            {
                var fileName = $"edevlet_{barcode}_{Guid.NewGuid():N}.pdf";
                var fullPath = Path.Combine(_storagePath, fileName);

                await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);
                return fullPath;
            }
        }

        throw new BusinessRuleException("Error_EGovernmentDocumentDownloadFailed");
    }

    // 🚀 Çerezleri elden geçirerek yollayan özel istek fırlatıcı
    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        Dictionary<string, string>? payload,
        CookieContainer cookieContainer,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);

        // 1. O anki kullanıcının çerezlerini isteğe ekle (İzolasyon)
        var cookieHeader = cookieContainer.GetCookieHeader(baseUri);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        // 2. Post işlemi ise form datayı ekle
        if (payload != null)
        {
            request.Content = new FormUrlEncodedContent(payload);
        }

        var response = await client.SendAsync(request, cancellationToken);

        // 3. Sunucudan dönen YENİ çerezleri yakala ve kutuya koy
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var cookieStr in setCookies)
            {
                // Domain uyuşmazlığı hatalarını önlemek için doğrudan URI'ye ekliyoruz
                cookieContainer.SetCookies(baseUri, cookieStr);
            }
        }

        return response;
    }

    private static string ExtractToken(string html, string step)
    {
        var match = Regex.Match(html, @"<body[^>]*data-token=[""']([^""']+)[""']", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Eğer e-Devlet'ten token gelmemişse WAF/Cloudflare tarafından engellenmişizdir.
        throw new BusinessRuleException("Error_EGovernmentSecurityTokenNotFound", step);
    }

    // 🚀 Bot koruması atlatıcı: Makine gibi saniyede 10 istek atmasını engeller.
    private static async Task HumanizeDelayAsync(CancellationToken cancellationToken)
    {
        // 400ms ile 1200ms arası rastgele bir bekleme süresi oluşturur
        int delay = Random.Shared.Next(400, 1200);
        await Task.Delay(delay, cancellationToken);
    }
}