using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;

/// <summary>
/// Command to validate, package, and finalize a completed registration draft.
/// </summary>
public record FinalizeRegistrationCommand(
    string RegistrationKey,
    int? ConfirmationMessageId = null
) : IRequest, IRegistrationCommand;
