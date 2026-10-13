using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;

public record CancelRegistrationCommand(
    string RegistrationKey,
    long ChatId
) : IRequest, IRegistrationCommand, IChatCommand;