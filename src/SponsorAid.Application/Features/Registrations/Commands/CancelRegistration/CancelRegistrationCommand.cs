using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;

/// <summary>
/// Command to cancel an active registration and clean up associated resources.
/// </summary>
public record CancelRegistrationCommand(
    string RegistrationKey,
    long ChatId
) : IRequest, IRegistrationCommand, IChatCommand;
