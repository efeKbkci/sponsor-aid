using System.Collections.Concurrent;
using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Behaviors;

/// <summary>
/// Pipeline behavior that serializes concurrent requests per registration key or chat identifier.
/// </summary>
public class RequestSerializationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string? lockKey = null;

        if (request is IRegistrationCommand regCmd)
        {
            lockKey = $"REG_{regCmd.RegistrationKey}";
        }
        else if (request is IChatCommand chatCmd)
        {
            lockKey = $"CHAT_{chatCmd.ChatId}";
        }

        if (lockKey == null)
        {
            return await next();
        }

        // Prevents race conditions and state corruption when a user sends rapid sequential messages.
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return await next();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
