using MediatR;
using Microsoft.Extensions.Logging;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Application.Behaviors;

public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDraftStateRepository _draftRepository;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(
        IDraftStateRepository draftRepository,
        IUserNotifier notifier,
        ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            // 1. İSTEĞİ HANDLER'A GÖNDER (next metodu Handler'ı çalıştırır)
            return await next();
        }
        catch (Exception ex)
        {
            // 2. EĞER HANDLER'DA HATA PATLARSA BURAYA DÜŞER

            long chatId;
            if (request is IRegistrationCommand registrationCommand)
            {
                var draft = await _draftRepository.GetAsync(registrationCommand.RegistrationKey, cancellationToken);
                if (draft == null) throw;

                draft.RevertProcessingState();
                await _draftRepository.SaveAsync(draft, cancellationToken);
                chatId = draft.ChatId;
            }
            else if (request is IChatCommand chatCommand)
            {
                var draft = await _draftRepository.GetActiveByChatIdAsync(chatCommand.ChatId, cancellationToken);
                if (draft != null)
                {
                    draft.RevertProcessingState();
                    await _draftRepository.SaveAsync(draft, cancellationToken);
                }

                chatId = chatCommand.ChatId;
            }
            else
            {
                throw;
            }

            // 3. HATAYI KİMLİKLE VE KULLANICIYA BİLDİR
            if (ex is SponsorAidException businessException)
            {
                // BİZİM FIRLATTIĞIMIZ İŞ KURALI HATALARI: Kullanıcıya kibarca ilet.
                await _notifier.SendMessageAsync(chatId, $"⚠️ İşlem Başarısız:\n{businessException.Message}");
            }
            else
            {
                // BEKLENMEYEN SİSTEM HATALARI (Örn: NullReference, Veritabanı kopması)
                // Hatayı kullanıcıya GÖSTERME. Sadece logla ve genel bir mesaj at.
                _logger.LogError(ex, "Sistem hatası oluştu. ChatId: {ChatId}", chatId);

                await _notifier.SendMessageAsync(chatId, "❌ Sunucu kaynaklı teknik bir sorun oluştu. Lütfen işlemi daha sonra tekrar deneyiniz.");
            }

            // 4. İŞLEMİ SESSİZCE BİTİR 
            // Hatayı en tepeye (API Controller'a) fırlatmıyoruz ki Controller Telegram'a "200 OK" dönebilsin 
            // ve Telegram aynı mesajı/belgeyi sürekli tekrar tekrar göndermesin.
            return default!;
        }
    }
}