using System.Net;
using System.Text.RegularExpressions;
using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Downloads verified official documents from the e-Government portal by handling multi-step verification.
/// </summary>
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
        // Reuses named HTTP client instance to prevent TCP socket exhaustion.
        var client = _httpClientFactory.CreateClient("EDevletClient");

        // Uses an isolated cookie container per request to prevent cross-session contamination.
        var cookieContainer = new CookieContainer();
        var baseUri = new Uri("https://www.turkiye.gov.tr/");

        // Step 1: Initial GET request to acquire initial session cookies and anti-forgery token.
        var response1 = await SendRequestAsync(client, HttpMethod.Get, "belge-dogrulama", null, cookieContainer, baseUri, cancellationToken);
        var html1 = await response1.Content.ReadAsStringAsync(cancellationToken);
        string token1 = ExtractToken(html1, "1. Adım");

        await HumanizeDelayAsync(cancellationToken);

        // Step 2: Posts document barcode to start verification sequence.
        var payload1 = new Dictionary<string, string> { { "sorgulananBarkod", barcode }, { "token", token1 }, { "btn", "Devam Et" } };
        var response2 = await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?submit", payload1, cookieContainer, baseUri, cancellationToken);
        var html2 = await response2.Content.ReadAsStringAsync(cancellationToken);
        string token2 = ExtractToken(html2, "2. Adım");

        await HumanizeDelayAsync(cancellationToken);

        // Step 3: Posts identity number associated with the document.
        var payload2 = new Dictionary<string, string> { { "ikinciAlan", idNumber }, { "token", token2 }, { "btn", "Devam Et" } };
        var response3 = await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?islem=dogrulama&submit", payload2, cookieContainer, baseUri, cancellationToken);
        var html3 = await response3.Content.ReadAsStringAsync(cancellationToken);
        string token3 = ExtractToken(html3, "3. Adım");

        await HumanizeDelayAsync(cancellationToken);

        // Step 4: Confirms consent to view and download document.
        var payload3 = new Dictionary<string, string> { { "chkOnay", "1" }, { "token", token3 }, { "btn", "Devam Et" } };
        await SendRequestAsync(client, HttpMethod.Post, "belge-dogrulama?islem=onay&submit", payload3, cookieContainer, baseUri, cancellationToken);

        await HumanizeDelayAsync(cancellationToken);

        // Step 5: Downloads the final generated PDF binary.
        var response5 = await SendRequestAsync(client, HttpMethod.Get, "belge-dogrulama?belge=goster&goster=1&display=display", null, cookieContainer, baseUri, cancellationToken);

        if (response5.IsSuccessStatusCode)
        {
            var fileBytes = await response5.Content.ReadAsByteArrayAsync(cancellationToken);

            // Validates that payload size exceeds error HTML responses (usually under 5 KB).
            if (fileBytes.Length > 5000)
            {
                var fileName = $"edevlet_{barcode}_{Guid.NewGuid():N}.pdf";
                var fullPath = Path.Combine(_storagePath, fileName);

                await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);
                return fullPath;
            }
        }

        throw new BusinessRuleException("Error_EGovernmentDocumentDownloadFailed");
    }

    // Handles request dispatch while maintaining isolated cookies for the active verification flow.
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

        // Injects current session cookies into the request header.
        var cookieHeader = cookieContainer.GetCookieHeader(baseUri);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        if (payload != null)
        {
            request.Content = new FormUrlEncodedContent(payload);
        }

        var response = await client.SendAsync(request, cancellationToken);

        // Extracts response cookies and stores them in the isolated session container.
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var cookieStr in setCookies)
            {
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

        // Fails when anti-forgery token is absent, indicating request rejection or rate limit.
        throw new BusinessRuleException("Error_EGovernmentSecurityTokenNotFound", step);
    }

    // Adds a randomized delay between requests to avoid automated rate limiting.
    private static async Task HumanizeDelayAsync(CancellationToken cancellationToken)
    {
        int delay = Random.Shared.Next(400, 1200);
        await Task.Delay(delay, cancellationToken);
    }
}
