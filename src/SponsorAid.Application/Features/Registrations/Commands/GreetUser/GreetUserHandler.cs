using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.GreetUser;

public class GreetUserHandler(IUserNotifier notifier) : IRequestHandler<GreetUserCommand>
{
    public Task Handle(GreetUserCommand request, CancellationToken cancellationToken)
    {
        return notifier.SendMessageAsync(request.ChatId, "Merhaba! Seçenekleri görmek için \"menü\" yazabilirsiniz.");
    }
}