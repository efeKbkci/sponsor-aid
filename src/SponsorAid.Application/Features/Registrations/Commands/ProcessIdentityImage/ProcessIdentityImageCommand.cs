using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

public record ProcessIdentityImageCommand(
    string RegistrationKey,
    string FileId,
    long FileSize,
    string Extension,
    string ImageSide // "front" veya "back" gelecek
) : IRequest, IRegistrationCommand;
