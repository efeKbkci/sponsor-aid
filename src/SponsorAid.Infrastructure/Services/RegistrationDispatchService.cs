using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Dispatches completed registration packages to reviewing authorities via email and out-of-band channels.
/// </summary>
public class RegistrationDispatchService(
    IConfiguration configuration,
    IUserNotifier notifier) : IRegistrationDispatchService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IUserNotifier _notifier = notifier;

    public async Task SendEncryptedPackageAsync(
        string registrationKey,
        string guardianName,
        byte[] packageBytes,
        CancellationToken cancellationToken = default)
    {
        var recipientEmail = _configuration["DISPATCH_EMAIL_RECIPIENT"]
            ?? Environment.GetEnvironmentVariable("DISPATCH_EMAIL_RECIPIENT")
            ?? throw new InvalidOperationException("DISPATCH_EMAIL_RECIPIENT tanımlanmamış.");

        var smtpHost = _configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp-relay.brevo.com";
        var smtpPort = int.Parse(_configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
        var smtpUser = _configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPass = _configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS");

        // Resolves sender address, defaulting to SMTP username when not configured explicitly.
        var smtpFrom = _configuration["SMTP_FROM"]
            ?? Environment.GetEnvironmentVariable("SMTP_FROM")
            ?? smtpUser;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SponsorAid Otomasyonu", smtpFrom));
        message.To.Add(new MailboxAddress("Yetkili İnceleme", recipientEmail));
        message.Subject = $"[Yeni Başvuru - Şifreli] {guardianName} - Takip No: {registrationKey}";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
                <h3>Yeni Yetim Bursu Başvurusu Tamamlandı</h3>
                <p><b>Vasi Adı:</b> {guardianName}</p>
                <p><b>Takip Kodu:</b> {registrationKey}</p>
                <p>Ekteki dosya <b>AES-256</b> algoritmasıyla şifrelenmiştir. Arşiv şifresi ayrı bir güvenlik kanalı üzerinden yetkiliye iletilmiştir.</p>"
        };

        bodyBuilder.Attachments.Add($"basvuru_{registrationKey}.zip", packageBytes, ContentType.Parse("application/zip"));
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        // Sets a short timeout to prevent blocking during mail relay connection delays.
        client.Timeout = 10000;
        await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrEmpty(smtpUser))
        {
            await client.AuthenticateAsync(smtpUser, smtpPass, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordAsync(
        string registrationKey,
        string guardianName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Sends archive decryption password through an out-of-band channel to prevent single-channel interception.
        var adminChatIdStr = _configuration["TELEGRAM_ADMIN_CHAT_ID"]
            ?? Environment.GetEnvironmentVariable("TELEGRAM_ADMIN_CHAT_ID");

        if (long.TryParse(adminChatIdStr, out var adminChatId))
        {
            var telegramAlert =
                $"🔐 <b>YENİ BAŞVURU ŞİFRESİ</b>\n\n" +
                $"👤 <b>Vasi:</b> {guardianName}\n" +
                $"🔑 <b>Takip Kodu:</b> <code>{registrationKey}</code>\n" +
                $"🛡️ <b>ZIP Parolası:</b> <code>{password}</code>\n\n" +
                $"<i>Bu şifre e-postadaki AES-256 şifreli arşivi açmak içindir. Lütfen üçüncü kişilerle paylaşmayınız.</i>";

            await _notifier.SendMessageAsync(adminChatId, telegramAlert);
        }
    }
}
