using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

/// <summary>
/// Represents a command to initialize a new registration session and generate its tracking key.
/// </summary>
public record StartRegistrationCommand(long ChatId) : IRequest<string>, IChatCommand;
