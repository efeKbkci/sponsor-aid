using System.Collections.Concurrent;
using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Behaviors;

public class RequestSerializationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Bellekte her kullanıcı/kayıt için asenkron bir kilit tutuyoruz
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string? lockKey = null;

        // İsteğin tipine göre kilit anahtarını belirliyoruz
        if (request is IRegistrationCommand regCmd)
        {
            lockKey = $"REG_{regCmd.RegistrationKey}";
        }
        else if (request is IChatCommand chatCmd)
        {
            lockKey = $"CHAT_{chatCmd.ChatId}";
        }

        // Eğer komut bu iki arayüzden birini uygulamıyorsa doğrudan geçir
        if (lockKey == null)
        {
            return await next();
        }

        // İlgili anahtar için kilit oluştur veya var olanı al
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        // Kapıda bekle: Benden önceki işlem veritabanı kaydını bitirene kadar bekle!
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Benden önceki bitti (veya kimse yoktu), içeri gir ve işlemi yap
            return await next();
        }
        finally
        {
            // İşlem bitti, sıradaki isteği içeri al
            semaphore.Release();
        }
    }
}