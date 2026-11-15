using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.GreetUser;

/// <summary>
/// Command to send the initial welcome message to the user.
/// </summary>
public record GreetUserCommand(long ChatId) : IRequest, IChatCommand;
