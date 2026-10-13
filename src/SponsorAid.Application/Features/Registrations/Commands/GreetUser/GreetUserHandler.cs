using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Resources;

namespace SponsorAid.Application.Features.Registrations.Commands.GreetUser;

public class GreetUserHandler(IUserNotifier notifier, IStringLocalizer<BotMessages> localizer) : IRequestHandler<GreetUserCommand>
{
    public Task Handle(GreetUserCommand request, CancellationToken cancellationToken)
    {
        return notifier.SendMessageAsync(request.ChatId, localizer["Prompt_Greeting"]);
    }
}