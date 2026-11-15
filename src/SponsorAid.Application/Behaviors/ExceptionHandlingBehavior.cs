using MediatR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Application.Behaviors;

/// <summary>
/// Pipeline behavior that catches unhandled exceptions, rolls back draft states, and notifies the user.
/// </summary>
public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDraftStateRepository _draftRepository;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;
    private readonly IStringLocalizer<BotMessages> _localizer;

    public ExceptionHandlingBehavior(
        IDraftStateRepository draftRepository,
        IUserNotifier notifier,
        ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger,
        IStringLocalizer<BotMessages> localizer)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            long chatId;
            if (request is IRegistrationCommand registrationCommand)
            {
                var draft = await _draftRepository.GetAsync(registrationCommand.RegistrationKey, cancellationToken);
                if (draft == null) throw;

                // Rolls back processing steps to allow the user to retry document submission.
                draft.RevertProcessingState();
                await _draftRepository.SaveAsync(draft, cancellationToken);
                chatId = draft.ChatId;
            }
            else if (request is IChatCommand chatCommand)
            {
                var draft = await _draftRepository.GetActiveByChatIdAsync(chatCommand.ChatId, cancellationToken);
                if (draft != null)
                {
                    // Rolls back processing steps to allow the user to retry document submission.
                    draft.RevertProcessingState();
                    await _draftRepository.SaveAsync(draft, cancellationToken);
                }

                chatId = chatCommand.ChatId;
            }
            else
            {
                throw;
            }

            // User-facing business errors display localized messages, while system errors are masked.
            if (ex is SponsorAidException businessException)
            {
                var localizedMessage = businessException.Arguments.Count == 0
                    ? _localizer[businessException.Message]
                    : _localizer[businessException.Message, businessException.Arguments.ToArray()];
                await _notifier.SendMessageAsync(chatId, _localizer["Error_OperationFailed", localizedMessage.Value].Value);
            }
            else
            {
                _logger.LogError(ex, "Sistem hatası oluştu. ChatId: {ChatId}", chatId);

                await _notifier.SendMessageAsync(chatId, _localizer["Error_UnexpectedSystemError"].Value);
            }

            // Swallows the error so the webhook returns a success status code and stops webhook retries.
            return default!;
        }
    }
}
