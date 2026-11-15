using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

/// <summary>
/// Represents a command to validate, download, and enqueue a front or back identity card photo for OCR processing.
/// </summary>
public record ProcessIdentityImageCommand(
    string RegistrationKey,
    string FileId,
    long FileSize,
    string Extension,
    string ImageSide
) : IRequest, IRegistrationCommand;
