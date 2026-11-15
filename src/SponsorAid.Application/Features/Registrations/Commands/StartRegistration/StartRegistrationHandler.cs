using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

/// <summary>
/// Handles creation of a new registration draft with a distinct, readable 6-character alphanumeric tracking key.
/// </summary>
public class StartRegistrationHandler(
    IDraftStateRepository draftRepository, 
    IRegistrationTrackingRepository trackingRepository,
    IUserNotifier notifier,
    IStringLocalizer<BotMessages> localizer
    ) : IRequestHandler<StartRegistrationCommand, string>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task<string> Handle(StartRegistrationCommand request, CancellationToken cancellationToken)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string generatedKey;

        // Requires a mix of letters and digits to prevent collision with pure numeric identifiers or words.
        do
        {
            generatedKey = RandomNumberGenerator.GetString(chars, 6);
        }
        while (generatedKey.All(char.IsDigit) || generatedKey.All(char.IsLetter));

        var newDraft = new RegistrationDraftState
        {
            RegistrationKey = generatedKey,
            ChatId = request.ChatId,
            CreatedAt = DateTime.UtcNow
        };

        await _draftRepository.SaveAsync(newDraft, cancellationToken);
        await _trackingRepository.CreateAsync(generatedKey, request.ChatId, cancellationToken);

        await _notifier.SendMessageAsync(request.ChatId, _localizer["Success_NewRegistrationStarted"]);
        return generatedKey;
    }
}
