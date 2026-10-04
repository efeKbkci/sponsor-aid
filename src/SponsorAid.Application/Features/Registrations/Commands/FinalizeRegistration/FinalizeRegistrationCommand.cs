using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;

// Command to finalize an existing registration. Fill properties as needed.
public record FinalizeRegistrationCommand(string RegistrationKey) : IRequest, IRegistrationCommand;
