using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.GreetUser;

public record GreetUserCommand(long ChatId) : IRequest, IChatCommand;